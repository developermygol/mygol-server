using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using CsvHelper;
using System.IO;
using CsvHelper.Configuration;
using webapi.Controllers;

namespace webapi.Importers
{
    public class PlayerImporter
    {
        // Consider password transformation (if available in plain text, we can import it). 

        public static ImportResult Import(IDbConnection c, string inputFile, long idCreator)
        {
            var result = new ImportResult();

            // TODO: 
            // - remove this, support upload. 
            // - support the case of empty fields. So far, height and weight cannot be empty, check with other numbers as well. 
            inputFile = @"C:\users\dsuar\Desktop\user sample data.csv";

            using (var r = File.OpenText(inputFile))
            {
                var csv = new CsvReader(r, new Configuration {
                    //HeaderValidated = (isValid, headerNames, headerNameIndex, context) => { },
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    ReadingExceptionOccurred = (ex) =>
                    {
                        result.Errors.Add(new ImportError
                        {
                            Line = ex.ReadingContext.RawRecord,
                            Error = ex.Message,
                            LineNumber = ex.ReadingContext.RawRow,
                            Column = ex.ReadingContext.CurrentIndex
                        });
                        result.NumRecordsWithError++;
                    }
                });
                
                var records = csv.GetRecords<Player>();

                var t = c.BeginTransaction();

                try
                {

                    foreach (var player in records)
                    {
                        var p = player.UserData.Password;

                        HashedPassword hashPass = (p != null) ? AuthTokenManager.HashPassword(p) : null;

                        // TODO: validate email / mobile doesn't exist already

                        PlayersController.InsertPlayer(c, t, player, idCreator, true, hashPass, UserEventType.PlayerImported);
                    }

                    t.Commit();
                }
                catch
                {
                    t.Rollback();
                }
            }

            return result;
        }
    }


    public class ImportResult
    {
        public int NumRecordsImported { get; set; }
        public int NumRecordsWithError { get; set; }
        public List<ImportError> Errors { get; } = new List<ImportError>();
    }


    public class ImportError
    {
        public string Line { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }
        public string Error { get; set; }
    }




}
