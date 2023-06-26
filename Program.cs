using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices //ignore, fixes compiler bug
{
    internal class IsExternalInit { }
}








namespace Past_Work
{
    internal static class DatabaseIntegrationExample
    {

        internal class DatabaseException : Exception
        {
            public DatabaseException(string message) : base(message) { }
        }

        internal readonly struct DateRange
        {
            private DateTime Start { get; init; }
            public string StartAsValidMySQL { get => Start.ToString("yyyy-MM-dd 00:00:00"); }
            private DateTime End { get; init; }
            public string EndAsValidMySQL { get => End.ToString("yyyy-MM-dd 23:59:59"); }

            public DateRange(DateTime Start, DateTime End)
            {
                this.Start = Start;
                this.End = End;
            }


            public DateRange(DateTime Start) : this(Start, Start) { }
        }
        internal static class DatabaseIntegration
        {
            public static string REPORTING_CONNECTION_STRING = ""; //removed for privacy reasons, connection string is for the 'reporting' user.
                                                                   //reporting user has access to all tables, but can only read. Has no write capabilities

            //inclusiveDates comes from a calendar
            public static IEnumerable<string[]> GenerateXeroReport(DateRange inclusiveDates)
            {
                //int j = 0;
                //while(j < 100)
                //{
                //    yield return new string[] { "a", "b", "c", "d", "e" };
                //    j += 1;
                //}
                //yield break;


                using MySqlConnection conn = new(REPORTING_CONNECTION_STRING);
                Task openConnection = conn.OpenAsync(); //open the connection asyncronously and do other independent work while we wait

                using MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "CALL FetchDataForXeroReport(@START_DATE, @END_DATE)";
                //stored procedure looks similar to: "SELECT * FROM invoices WHERE date_paid BETWEEN @START_DATE AND @END_DATE"
                //but with appropriate columns specified

                cmd.Parameters.AddWithValue("@START_DATE", inclusiveDates.StartAsValidMySQL);
                cmd.Parameters.AddWithValue("@END_DATE", inclusiveDates.EndAsValidMySQL);

                while (!openConnection.IsCompleted)
                    ;
                //ensure connection is open -> async streams not supported in framework 4.8 -> busy wait.
                //Worst case scenario this executes in the same time as the synchronous method
                //Server is local so worst case is still very fast

                if (openConnection.IsFaulted || openConnection.IsCanceled)
                    throw new DatabaseException("Unable to establish connection to database.");


                using MySqlDataReader reader = cmd.ExecuteReader(); //no point using async, we can't start executing until openConnection returns and we have no other work to do while we wait.
                                                                    //better to avoid the overhead of creating tasks

                //because query is called from within stored procedure, we fetch the names and types of columns to ensure stability if procedure changes.
                //we create a delegate to hold the parsing method for that type so we can make appropriate conversions
                string[] fields = new string[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fields[i] = reader.GetName(i);
                }

                yield return fields; //send our column headers out to be processed

                while (reader.Read())
                {
                    string[] rowData = new string[reader.FieldCount];
                    for (int column = 0; column < fields.Length; column++)
                    {
                        rowData[column] = reader.GetString(fields[column]);
                    }
                    yield return rowData; //send this row to be processed
                }

                yield break;
            }
        }

        internal class Usage
        {
            static void Main(string[] args)
            {
                DateTime start = DateTime.Now.AddDays(-10);
                DateTime end = DateTime.Now;
                DateRange range = new(start, end);

                string dir = Directory.GetCurrentDirectory();
                string fileName = "example_path.csv";
                string path = Path.Combine(dir, fileName);

                using StreamWriter writer = new(path);
                foreach (string[] row in DatabaseIntegration.GenerateXeroReport(range)) //returns 1 row at a time, more performant than returning list or array for potentially large datasets
                {
                    //do whatever, e.g.
                    writer.WriteLine(string.Join(", ", row)); //use a streamwriter to better handle large datasets
                }
                writer.Close();
                Process.Start(path);
                Console.ReadLine();
            }
        }
    }
    internal class Program
    {
        
    }
}
