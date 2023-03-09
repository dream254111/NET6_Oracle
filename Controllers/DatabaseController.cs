using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;

namespace TestOracleDatabase.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatabaseController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DatabaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("oracle")]
        public IActionResult ExecuteSP()
        {
            string spName = string.Empty;
            Dictionary<string, string> spParam = new();

            foreach (string entry in Request.Form.Keys)
            {
                if (entry == "SPName") spName = Request.Form[entry];
                else spParam.Add($"PI_{entry}", Request.Form[entry]);
            }

            if (string.IsNullOrEmpty(spName))
                return BadRequest("No SPName in request");

            try
            {
                List<Dictionary<string, string>> response = new();

                using (OracleConnection connection = new(_configuration.GetConnectionString("YumenoTest")))
                {
                    connection.Open();

                    OracleCommand command = new(spName, connection)
                    {
                        BindByName = true,
                        CommandType = CommandType.StoredProcedure
                    };

                    foreach (KeyValuePair<string, string> entry in spParam)
                    {
                        command.Parameters.Add(new()
                        {
                            ParameterName = entry.Key,
                            Value = entry.Value,
                            Direction = ParameterDirection.Input,
                            OracleDbType = OracleDbType.NVarchar2,
                            Size = 4000
                        });
                    }

                    command.Parameters.Add(new()
                    {
                        ParameterName = "PO_DATA",
                        Direction = ParameterDirection.Output,
                        OracleDbType = OracleDbType.RefCursor
                    });
                    command.Parameters.Add(new()
                    {
                        ParameterName = "PO_STATUS",
                        Direction = ParameterDirection.Output,
                        OracleDbType = OracleDbType.NVarchar2,
                        Size = 4000
                    });
                    command.Parameters.Add(new()
                    {
                        ParameterName = "PO_STATUS_MSG",
                        Direction = ParameterDirection.Output,
                        OracleDbType = OracleDbType.NVarchar2,
                        Size = 4000
                    });

                    using (OracleDataReader reader = command.ExecuteReader())
                    {
                        List<string> columns = new();

                        foreach (DbColumn column in reader.GetColumnSchema())
                        {
                            columns.Add(column.ColumnName);
                        }

                        while (reader.Read())
                        {
                            Dictionary<string, string> record = new();
                            foreach (string column in columns)
                            {
                                record.Add(column, reader[column].ToString() ?? string.Empty);
                            }
                            response.Add(record);
                        }
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
            }
        }
    }
}
