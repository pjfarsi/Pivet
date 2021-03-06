﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Pivet.Data.Connection
{
    internal class BootstrapConnection : IConnectionProvider
    {
        ConnectionConfig connParams;

        public Tuple<OracleConnection, bool, string> GetConnection()
        {
            if (connParams == null)
            {
                return new Tuple<OracleConnection, bool, string>(null, false, "Parameters not set.");
            }

            if (connParams.TNS_ADMIN.Length > 0)
            {
                Environment.SetEnvironmentVariable("TNS_ADMIN", connParams.TNS_ADMIN);
            }

            OracleConnection conn = new OracleConnection($"Data Source={connParams.TNS};User Id={connParams.BootstrapParameters.User}; Password={connParams.BootstrapParameters.Password};Connection Timeout=120");

            try
            {
                conn.Open();
                if (connParams.Schema != null && connParams.Schema.Length > 0)
                {
                    /* switch our schema */
                    OracleCommand schemaCommand = new OracleCommand($"ALTER SESSION SET CURRENT_SCHEMA={connParams.Schema}", conn);
                    schemaCommand.ExecuteNonQuery();
                }
                return new Tuple<OracleConnection, bool, string>(conn, true, "");
            }
            catch (Exception ex)
            {
                return new Tuple<OracleConnection, bool, string>(null, false, "Failed to get oracle connection: " + ex.Message);
            }
        }

        public void SetParameters(ConnectionConfig parms)
        {
            connParams = parms;
        }
    }
}
