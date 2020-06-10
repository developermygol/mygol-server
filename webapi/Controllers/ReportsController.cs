using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using webapi.Models.Db;
using webapi.Payment;

namespace webapi.Controllers
{
    public class ReportsController: DbController
    {
        public ReportsController(IOptions<Config> config) : base(config)
        {

        }


        [HttpGet("insurance")]
        public IActionResult GetPlayerInsuranceReport()
        {
            // Nombre Apellidos NIF Población Provincia Fecha de nacimiento Opción de seguro elegida

            StringBuilder csv = null;

            DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                var query = @"
                    SELECT p.id as ""ID"", p.name as ""Nombre"", p.surname as ""Apellidos"", p.city as ""Ciudad"", p.state as ""Provincia"", p.idCardNumber as ""DNI"", p.birthDate as ""Fecha nacimiento"", t.name as ""Equipo"", tp.enrollmentdate as ""Fecha inscripción"",  tp.enrollmentPaymentData as ""Stripe transaction"", tp.enrollmentData as ""Opciones Inscripción""
                    FROM players p JOIN teamplayers tp ON p.id = tp.idPlayer JOIN teams t on t.id = tp.idTeam 
                    WHERE tp.enrollmentPaymentData is not null ORDER BY tp.enrollmentDate ASC";

                csv = GetCsvFromQuery(c, query,
                    (i, value) =>
                    {
                        var r = value.ToString();
                        if (i == 10) return ProcessEnrollmentData(r);

                        return r;
                    });

                return null;
            });

            return GetFileContentResult(csv.ToString(), "insurance.csv");
        }

        [HttpGet("allplayers")]
        public IActionResult GetAllPlayers()
        {
            // Nombre Apellidos NIF Población Provincia Fecha de nacimiento Opción de seguro elegida

            StringBuilder csv = null;

            DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                var query = @"
                    SELECT p.id as ""ID"", p.name as ""Nombre"", p.surname as ""Apellidos"",p.city as ""Ciudad"", p.state as ""Provincia"", p.idCardNumber as ""DNI"", p.birthDate as ""Fecha nacimiento"", t.name as ""Equipo"", tp.fieldPosition as ""Posición"", tp.enrollmentdate as ""Fecha inscripción"",  tp.enrollmentPaymentData as ""Stripe transaction"", tp.enrollmentData as ""Opciones Inscripción""
                    FROM players p LEFT JOIN teamplayers tp ON p.id = tp.idPlayer JOIN teams t on t.id = tp.idTeam
                    ORDER BY p.id ASC";

                csv = GetCsvFromQuery(c, query,
                    (i, value) =>
                    {
                        var r = value.ToString();
                        if (i == 8) return GetPositionForIndex(r);
                        if (i == 11) return ProcessEnrollmentData(r);


                        return r;
                    });

                return null;
            });

            return GetFileContentResult(csv.ToString(), "insurance.csv");
        }

        private static FileContentResult GetFileContentResult(string content, string fileName)
        {
            var contentType = "text/plain";
            var bytes = Encoding.GetEncoding(1252).GetBytes(content);
            var result = new FileContentResult(bytes, contentType)
            {
                FileDownloadName = fileName
            };

            return result;
        }


        private static string GetPositionForIndex(string index)
        {
            switch (index)
            {
                case "0": return "Sin posición";
                case "1": return "Portero";
                case "2": return "Defensa";
                case "3": return "Centrocampista";
                case "4": return "Delantero";
                case "5": return "Delegado no jugador";
                case "6": return "Entrenador";
                default: return "Sin definir";
            }
        }

        private static string ProcessEnrollmentData(string val)
        {
            if (val == null || val == "") return "";

            try
            {
                var epm = EnrollmentPaymentData.Hydrate(val);
                if (epm == null) return "";

                var result = new StringBuilder();
                var isFirst = true;

                foreach (var step in epm.Steps)
                {
                    if (isFirst) isFirst = false; else result.Append(",");

                    result.Append(GetCsvValue($"{step.Title}: {step.SelectedOption?.Title}"));
                    result.Append(", ");
                    result.Append(GetCsvValue($"{step.SelectedOption?.Price}"));
                }

                return result.ToString();
            }
            catch
            {
                return "";
            }
        }

        //private static string ProcessEnrollmentTitle(object val)
        //{
        //    if (val == null) return "";

        //    try
        //    {
        //        var epm = EnrollmentPaymentData.Hydrate(val.ToString());
        //        if (epm == null) return "";

        //        var result = new StringBuilder();
        //        var isFirst = true;

        //        foreach (var step in epm.Steps)
        //        {
        //            if (isFirst) isFirst = false; else result.Append(",");

        //            result.Append(GetCsvValue(step.Title));
        //        }

        //        return result.ToString();
        //    }
        //    catch
        //    {
        //        return "";
        //    }
        //}


        // __ CSV generation __________________________________________________


        public static StringBuilder GetCsvFromQuery(IDbConnection c, string query, Func<int, object, string> fieldCallback = null, Func<int, string, object, string> headerCallback = null)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = query;
                var reader = cmd.ExecuteReader();

                var result = new StringBuilder();
                var isFirst = true;

                result.AppendLine("sep=,");  // For excel

                while (reader.Read())
                {
                    if (isFirst)
                    {
                        result.AppendLine(GetCsvHeader(reader, headerCallback));
                        isFirst = false;
                    }

                    result.AppendLine(GetCsvLine(reader, fieldCallback));
                }

                return result;
            }
        }

        public static string GetCsvHeader(IDataReader reader, Func<int, string, object, string> headerCallback)
        {
            bool isFirst = true;
            var sb = new StringBuilder();

            for (int i = 0; i < reader.FieldCount; ++i)
            {
                var title = reader.GetName(i);
                var value = reader.GetValue(i);
                title = (headerCallback != null) ? headerCallback(i, title, value) : GetCsvValue(title);

                if (isFirst)
                {
                    sb.Append(title);
                    isFirst = false;
                }
                else
                {
                    sb.Append("," + title);
                }
            }

            return sb.ToString();
        }

        public static string GetCsvLine(IDataReader reader, Func<int, object, string> fieldCallback)
        {
            bool isFirst = true;
            var sb = new StringBuilder();

            for (int i = 0; i < reader.FieldCount; ++i)
            {
                object val = reader.GetValue(i);
                val = (fieldCallback != null) ? fieldCallback(i, val) : GetCsvValue(val.ToString());

                if (isFirst)
                {
                    sb.Append(val);
                    isFirst = false;
                }
                else
                {
                    sb.Append("," + val);
                }
            }

            return sb.ToString();
        }


        private static string GetCsvValue(string val)
        {
            if (val == null) return "";

            val = val.Replace("\"", "\\\"");

            if (val.Contains(',') || val.Contains(' ')) return $"\"{val}\"";

            return val;
        }
    }
}
