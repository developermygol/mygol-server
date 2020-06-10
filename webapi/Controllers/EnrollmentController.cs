using Dapper;
using Dapper.Contrib.Extensions;
using Ganss.XSS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using Stripe;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using webapi.Models.Db;
using webapi.Models.Result;
using webapi.Payment;

namespace webapi.Controllers
{

    public class EnrollmentController : DbController
    {
        public EnrollmentController(IOptions<Config> config, NotificationManager notif, AuthTokenManager authManager) : base(config)
        {
            mNotifications = notif;
            mAuthTokenManager = authManager;
        }




        [HttpPost("step")]
        public IActionResult SaveEnrollmentData([FromBody] EnrollmentData enroll)
        {
            return DbTransaction((c, t) =>
            {
                if (enroll == null) throw new NoDataException();
                if (!IsLoggedIn() && enroll.IdStep > 2) throw new UnauthorizedAccessException();

                Audit.Information(this, "{0}: Players.EnrollmentStep: {1}", GetUserId(), enroll.IdStep);

                switch (enroll.IdStep)
                {
                    case 2: return ProcessStep2(c, t, enroll);
                    case 3: return ProcessStep3(c, t, enroll);
                    case 4: return ProcessStep4(c, t, enroll);
                    case 10: return ProcessStep10(c, t, enroll);
                    case 20: return ProcessStep20(c, t, enroll);
                    case 21: return ProcessStep21(c, t, enroll).Run();
                    case 100: return ProcessStep100(c, t, enroll);
                    case 101: return ProcessStep101(c, t, enroll);
                    default: throw new Exception("Error.NoStep");
                }
            });
        }

        private int ProcessStep2(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            // at this point, user has to be logged in (even if there is no password yet, only used the enrollment PIN). 
            // Save data, encode password

            var result = 1;

            var dbPlayer = GetPlayer(c, t);

            if (!dbPlayer.Approved)
            {
                if (dbPlayer.Name != enroll.Name || dbPlayer.Surname != enroll.Surname)
                {
                    // update user record too
                    c.Execute("UPDATE users SET name = @name WHERE id = @idUser", new { idUser = GetUserId(), name = enroll.Name + " " + enroll.Surname }, t);
                }

                dbPlayer.Name = enroll.Name;
                dbPlayer.Surname = enroll.Surname;
                dbPlayer.Address1 = enroll.Address1;
                dbPlayer.Address2 = enroll.Address2;
                dbPlayer.City = enroll.City;
                dbPlayer.State = enroll.State;
                dbPlayer.CP = enroll.CP;
                dbPlayer.Country = enroll.Country;
                dbPlayer.IdCardNumber = enroll.IdCardNumber;
                dbPlayer.BirthDate = enroll.BirthDate;

                if (!enroll.IsEditing) dbPlayer.EnrollmentStep = 3;

                c.Update(dbPlayer, t);

                result = 2;
            }

            // Join this to the case above that also edits the user, so only db update is needed. 
            if (enroll.Password != null && enroll.Password != "")
            {
                var dbUser = c.Get<User>(GetUserId(), t);
                UsersController.UpdatePassword(dbUser, enroll.Password);
                c.Update(dbUser, t);
            }

            return result;
        }

        

        private bool ProcessStep3(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            // Save the enrollmentStep and idpicture url

            var dbPlayer = GetPlayer(c, t);

            if (!enroll.IsEditing)
            {
                dbPlayer.EnrollmentStep = 4;
            }

            if (!dbPlayer.Approved)
            {
                // Get the latest upload from the DB instead of enroll.FichaPictureImgUrl. Don't trust the client (and the app doesn't send it anyway)
                dbPlayer.IdPhotoImgUrl = GetLastUploadUrlForType(c, t, GetUserId(), UploadType.PlayerIdPhoto);
            }

            c.Update(dbPlayer, t);

            return true;
        }

        private bool ProcessStep4(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            var dbPlayer = GetPlayer(c, t);
            var dbTeamPlayer = GetTeamPlayer(c, t, enroll.IdTeam);

            if (!dbPlayer.Approved)
            {
                // Get the latest upload from the DB instead of enroll.IdCardXImgUrl. Don't trust the client (and the app doesn't send it anyway)
                dbPlayer.IdCard1ImgUrl = GetLastUploadUrlForType(c, t, GetUserId(), UploadType.PlayerIdCard1);
                dbPlayer.IdCard2ImgUrl = GetLastUploadUrlForType(c, t, GetUserId(), UploadType.PlayerIdCard2);
            }

            dbTeamPlayer.Status |= (int)TeamPlayerStatusFlags.RegistrationCompleted;
            dbTeamPlayer.Status |= (int)TeamPlayerStatusFlags.IdCardUploaded;

            if (!enroll.IsEditing)
            {
                dbPlayer.EnrollmentStep = 10;
                dbTeamPlayer.EnrollmentStep = 10;
            }

            c.Update(dbPlayer, t);
            c.Update(dbTeamPlayer, t);

            return true;
        }

        private bool ProcessStep10(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            // Save the selected payment options? or do nothing until there is a payment result. 
            // Maybe to store the insurance declaration ? But then, there is already an upload...
            return true;
        }

        private bool ProcessStep20(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            // Save the payment data
            var dbTeamPlayer = GetTeamPlayer(c, t, enroll.IdTeam);

            dbTeamPlayer.EnrollmentData = enroll.SelectedOptionsJson;
            
            // Do not change the enrollment step so if the app rehydrates, it will go back to selecting the options. 
            //dbTeamPlayer.EnrollmentStep = 20;       // We stay on 20 instead of moving to the next screen so if the app rehydrates here, it will show the payment summary screen.
            c.Update(dbTeamPlayer, t);

            return true;
        }




        private async Task<bool> ProcessStep21(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            var dbTeamPlayer = GetTeamPlayer(c, t, enroll.IdTeam);

            // Calculate the amount of selected options (from teamData.enrollmentData)
            // To do that, get the aplicable workflow to this player and team
            var (total, selectedOptionsTotal) = GetPaymentAmounts(c, t, GetUserId(), enroll.IdTeam, enroll.IdTournament, dbTeamPlayer.EnrollmentData);
            if (selectedOptionsTotal > 0)
            {
                // Total before fees is more than 0, process charge.
                var amount = total;

                Audit.Information(this, "{0}: Players.Enrollment: Payment: {0}", GetUserId(), amount);

                // We get the card token from the client
                var cardToken = enroll.PaymentGatewayResult;
                if (cardToken == null) throw new Exception("Error.NoCardToken");

                var orgSecrets = GetOrgWithSecrets(c, t);

                // Now, call the payment gateway with the card token and amount. 
                var charge = await StripeApi.SendCharge(orgSecrets.PaymentKey, cardToken, orgSecrets.PaymentCurrency, amount, orgSecrets.PaymentDescription);
                if (!charge.Paid) throw new Exception("Error.PaymentFailed");

                dbTeamPlayer.EnrollmentPaymentData = charge.Id;

                // Add user event for payment
                LogPaymentEvent(c, t, charge, enroll.IdPlayer, enroll.IdTeam, enroll.IdTournament);

                // Notify org admins there is a new enrollment pending verification
                var (userName, teamName, tournamentName) = GetPlayerTeamTournament(c, t, GetUserId(), dbTeamPlayer.IdTeam, enroll.IdTournament);

                mNotifications.NotifyAdminsGenericEmail(Request, c, t, GetUserId(),
                    Localization.Get("Nuevo pago de jugador", null),
                    Localization.Get("El jugador '{0}' ha hecho un pago de inscripción de {1:0.00} {2} en el equipo '{3}', torneo '{4}'", null,
                        userName, amount, orgSecrets.PaymentCurrency, teamName, tournamentName),
                        null);
            }

            // Set player status as paid. If amount before fees was 0, we still do this to move to next step.
            dbTeamPlayer.Status |= (int)TeamPlayerStatusFlags.Paid;
            dbTeamPlayer.EnrollmentStep = 100;
            dbTeamPlayer.EnrollmentDate = DateTime.Now;
            c.Update(dbTeamPlayer, t);

            return true;
        }

        private static (string, string, string) GetPlayerTeamTournament(IDbConnection c, IDbTransaction t, long idUser, long idTeam, long idTournament)
        {
            var query = @"
                SELECT name FROM users WHERE id = @idUser;
                SELECT name FROM teams WHERE id = @idTeam;
                SELECT name FROM tournaments WHERE id = @idTournament;
            ";

            var qr = c.QueryMultiple(query, new { idUser, idTeam, idTournament }, t);

            return (
                GetStringOrUnknown(qr.ReadSingleOrDefault<string>()),
                GetStringOrUnknown(qr.ReadSingleOrDefault<string>()),
                GetStringOrUnknown(qr.ReadSingleOrDefault<string>())
            );
        }

        private static string GetStringOrUnknown(string value)
        {
            if (value != null) return value;

            return Localization.Get("desconocido", null);
        }

        private static (double, double) GetPaymentAmounts(IDbConnection c, IDbTransaction t, long idUser, long idTeam, long idTournament, string enrollmentData)
        {
            var workflowConfig = PaymentConfigsController.GetEnrollmentPaymentWorkflowForUser(c, t, idTeam, idTournament, idUser);
            if (workflowConfig == null) throw new Exception("Error.PaymentWorkflowNotFound");

            var workFlow = EnrollmentPaymentWorkflow.Hydrate(workflowConfig.EnrollmentWorkflow);
            if (workFlow == null) throw new Exception("Error.InvalidPaymentWorkflow");

            // DAVE: add organization payment options to workflow object here (from organization manager, using the request).

            var enrollWorkflowData = EnrollmentPaymentData.Hydrate(enrollmentData);
            if (enrollWorkflowData == null) throw new Exception("Error.InvalidPlayerTeamEnrollmentData");

            var result = new double[2];
            var total = enrollWorkflowData.GetTotal(workFlow);                  // Total with fees
            var selectedOptionsTotal = enrollWorkflowData.GetSelectedOptionsTotal(workFlow);   // Total before fees

            return (total, selectedOptionsTotal);
        }

        private bool ProcessStep100(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            var dbTeamPlayer = GetTeamPlayer(c, t, enroll.IdTeam);
            
            dbTeamPlayer.EnrollmentStep = 101;
            c.Update(dbTeamPlayer, t);

            // NOTIFY org admin that a new player has paid enrollment. 

            return true;
        }

        private bool ProcessStep101(IDbConnection c, IDbTransaction t, EnrollmentData enroll)
        {
            var dbPlayer = GetPlayer(c, t);

            if (!string.IsNullOrWhiteSpace(enroll.AvatarImgUrl))
            {
                // Update the userdata record
                c.Execute("UPDATE users SET avatarImgUrl = @img WHERE id = @idUser", new { idUser = GetUserId(), img = enroll.AvatarImgUrl }, t);
            }

            //dbPlayer.AvatarImgUrl = enroll.AvatarImgUrl;
            dbPlayer.Height = enroll.Height;
            dbPlayer.Weight = enroll.Weight;
            dbPlayer.Motto = enroll.Motto;
            dbPlayer.FacebookKey = enroll.FacebookKey;
            dbPlayer.InstagramKey = enroll.InstagramKey;
            dbPlayer.TwitterKey = enroll.TwitterKey;

            //if (!enroll.IsEditing) dbPlayer.EnrollmentStep = 102;

            c.Update(dbPlayer, t);


            if (enroll.IsEditing) return true;

            var dbTeamPlayer = GetTeamPlayer(c, t, enroll.IdTeam);

             dbTeamPlayer.EnrollmentStep = 102;

            c.Update(dbTeamPlayer, t);
            return true;
        }


        private Player GetPlayer(IDbConnection c, IDbTransaction t)
        {
            var dbPlayers = c.Query<Player>("SELECT * FROM players WHERE idUser = @idUser", new { idUser = GetUserId() }, t);
            if (dbPlayers == null || dbPlayers.Count() == 0) throw new Exception("Error.NotFound");
            if (dbPlayers.Count() > 1) throw new Exception("Error.MoreThanOnePlayerForThisUser");

            var result = dbPlayers.First();
            if (result == null) throw new Exception("Error.PlayerNotFound");

            return result;
        }

        private TeamPlayer GetTeamPlayer(IDbConnection c, IDbTransaction t, long idTeam)
        {
            var dbTeams = c.Query<TeamPlayer>("SELECT tp.* FROM teamplayers tp JOIN players p ON p.id = tp.idPlayer WHERE p.idUser = @idUser AND tp.idTeam = @idTeam", new { idUser = GetUserId(), idTeam = idTeam }, t);
            var count = dbTeams.Count();
            if (count == 0) throw new Exception("Error.TeamNotFound");
            if (count > 1) throw new Exception("Error.DuplicatedPlayerInTeam");

            var dbTeam = dbTeams.First();

            return dbTeam;
        }

        private OrganizationWithSecrets GetOrgWithSecrets(IDbConnection c, IDbTransaction t)
        {
            var orgSecrets = c.Query<OrganizationWithSecrets>("SELECT * FROM organizations", null, t).FirstOrDefault();
            if (orgSecrets == null) throw new Exception("Error.NoOrg");
            if (String.IsNullOrWhiteSpace(orgSecrets.PaymentCurrency) ||
                String.IsNullOrWhiteSpace(orgSecrets.PaymentKey) ||
                String.IsNullOrWhiteSpace(orgSecrets.PaymentKeyPublic) ||
                String.IsNullOrWhiteSpace(orgSecrets.PaymentDescription)) throw new Exception("Error.Organization.PaymentGatewayNotConfigured");

            return orgSecrets;
        }

        private void LogPaymentEvent(IDbConnection c, IDbTransaction t, StripeCharge charge, long idPlayer, long idTeam, long idTournament)
        {
            var ev = new UserEvent
            {
                IdCreator = GetUserId(),
                IdUser = GetUserId(),
                TimeStamp = DateTime.Now,
                Description = Localization.Get("Pago de inscripción", null),
                Type = (int)UserEventType.PlayerPaymentSuccess,
                Data1 = charge.Id,
                Data2 = charge.Amount.ToString(),
                Data3 = idTeam.ToString()
            };

            c.Insert(ev, t);
        }

        private string GetLastUploadUrlForType(IDbConnection c, IDbTransaction t, long idUser, UploadType type)
        {
            var sql = "SELECT repositorypath FROM uploads WHERE idobject = @idUser AND type = @type ORDER BY id DESC LIMIT 1";
            var result = c.ExecuteScalar<string>(sql, new { idUser, type = (int)type }, t);

            return result;
        }

        private NotificationManager mNotifications;
        private AuthTokenManager mAuthTokenManager;
    }


    public class EnrollmentData
    {
        // Required

        public int IdStep { get; set; }
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        public long IdTournament { get; set; }

        // Optional but global: 
        public bool IsEditing { get; set; }
        

        // Step 1: Pin screen

        public string EnrollPin { get; set; }

        // Step 2

        public string Name { get; set; }
        public string Surname { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string CP { get; set; }
        public string Country { get; set; }
        public string IdCardNumber { get; set; }
        public DateTime BirthDate { get; set; }
        public string Password { get; set; }

        // Step 3

        public string FichaPictureImgUrl { get; set; }

        // Step 4

        public string IdCard1ImgUrl { get; set; }
        public string IdCard2ImgUrl { get; set; }

        // Step 10

        public string SelectedOptionsJson { get; set; }

        // Step 21

        public string PaymentGatewayResult { get; set; }

        // Step 101

        public string AvatarImgUrl { get; set; }
        public long Height { get; set; }          // Purposedly string so it can be nullable
        public long Weight { get; set; }          // Purposedly string so it can be nullable
        public string Motto { get; set; }
        public string FacebookKey { get; set; }
        public string InstagramKey { get; set; }
        public string TwitterKey { get; set; }


    }


}




