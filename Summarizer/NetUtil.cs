using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sinotech.Mis.Extensions.Configuration;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Summarizer
{
    /// <summary>
    /// 網路相關工具類別，提供網域、主機、IP、員工資訊等取得方法
    /// </summary>
    public static class NetUtil
    {
        /// <summary>
        /// 取得網域名稱 (例如: "SINOTECH\\user" 會回傳 "SINOTECH")
        /// </summary>
        /// <param name="username">使用者帳號 (格式: DOMAIN\\username)</param>
        /// <returns>網域名稱 (大寫)</returns>
        public static string GetDomain(string username)
        {
            // 取出 \\ 前的字串並轉大寫
            return username.Substring(0, username.IndexOf('\\')).ToUpper();
        }

        /// <summary>
        /// 取得本機主機名稱 (HostName)
        /// </summary>
        /// <returns>本機主機名稱，若失敗則回傳 null</returns>
        public static string? GetLocalHostName()
        {
            string? ret = null; // 回傳主機名稱
            try
            {
                // 取得本機主機名稱
                string hostName = Dns.GetHostName();
                ret = hostName;
            }
            catch (SocketException ex)
            {
                // 捕捉網路錯誤
                Console.WriteLine("CardUtility.GetLocalHostName: SocketException caught!!!");
                Console.WriteLine("Source : " + ex.Source);
                Console.WriteLine("Message : " + ex.Message);
            }
            catch (Exception ex)
            {
                // 捕捉其他例外
                Console.WriteLine("CardUtility.GetLocalHostName: Exception caught!!!");
                Console.WriteLine("Source : " + ex.Source);
                Console.WriteLine("Message : " + ex.Message);
            }
            return ret;
        }

        /// <summary>
        /// 取得員工姓名、信箱、部門編號、部門名稱
        /// </summary>
        /// <param name="empNo">員工編號</param>
        /// <param name="configuration">組態設定 (IConfiguration)</param>
        /// <returns>員工姓名、信箱、部門編號、部門名稱 (Tuple)</returns>
        public static (string name, string email, int deptNo, string deptName) GetDeptEtc(string empNo, IConfiguration configuration)
        {
            // SQL 查詢語法
            string sql = @"SELECT CName,DeptNo,DeptSName,Email FROM MIS.dbo.vwEmpData WHERE EmpNo = @EmpNo";
            // 取得連線字串
            string? connStr = configuration.GetConnectionString("MIS");
            if (string.IsNullOrEmpty(connStr))
            {
                throw new Exception("找不到 MIS 的連線字串");
            }
            // 解密連線字串
            connStr = RsaCrypto.RsaDecrypt(connStr);

            // 員工資訊變數
            string name = string.Empty;      // 員工姓名
            int deptNo = 0;                  // 部門編號
            string deptName = string.Empty;  // 部門名稱
            string email = string.Empty;     // 員工信箱

            if (string.IsNullOrEmpty(connStr))
            {
                throw new Exception("找不到 HarassmentComplaint 的連線字串");
            }

            // 建立 SQL 連線
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.Open();
                try
                {
                    // 建立 SQL 指令
                    SqlCommand command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@EmpNo", empNo);

                    // 執行查詢
                    SqlDataReader dr = command.ExecuteReader();
                    if (dr.HasRows)
                    {
                        dr.Read();
                        // 取得姓名
                        name = dr["CName"]?.ToString() ?? string.Empty;
                        // 取得部門名稱
                        deptName = dr["DeptSName"]?.ToString() ?? string.Empty;

                        // 取得信箱 (若無則給預設值)
                        if (dr["Email"] != DBNull.Value)
                        {
                            email = dr["Email"]?.ToString() ?? string.Empty;
                        }
                        else
                        {
                            // 若無 Email，則使用預設 Email
                            email = "noreply@sinotech.org.tw";
                        }
                        // 取得部門編號 (轉為整數)
                        deptNo = Convert.ToInt32(dr["DeptNo"].ToString());
                    }
                }
                finally
                {
                    // 關閉連線
                    if (connection.State == System.Data.ConnectionState.Open)
                    {
                        connection.Close();
                    }
                }
            }
            return (name, email, deptNo, deptName);
        }

        /// <summary>
        /// 取得用戶端 IP 與主機名稱
        /// </summary>
        /// <param name="context">HttpContext</param>
        /// <returns>IP 位址與主機名稱 (Tuple)</returns>
        public static (IPAddress?, string?) GetIP_Hostname(HttpContext context)
        {
            IPAddress? ipAddress = null; // 用戶端 IP
            string? hostname = null;      // 主機名稱
            // 檢查 context 與連線資訊
            if (context != null && context.Connection != null && context.Connection.RemoteIpAddress != null)
            {
                ipAddress = context.Connection.RemoteIpAddress;

                // 若為 IPv4 映射 IPv6，轉回 IPv4
                if (ipAddress.IsIPv4MappedToIPv6)
                {
                    ipAddress = ipAddress.MapToIPv4();
                }

                // 若為本機回送位址 (127.0.0.1)
                if (IPAddress.IsLoopback(ipAddress))
                {
                    try
                    {
                        // 取得本機 IPv4 位址
                        string ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(o => o.AddressFamily == AddressFamily.InterNetwork).ToString();
                        ipAddress = IPAddress.Parse(ip);
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine($"{ex.Message} {ex.StackTrace}");
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine($"{ex.Message} {ex.StackTrace}");
                    }
                    // 取得本機主機名稱
                    hostname = GetLocalHostName();
                }
                else
                {
                    try
                    {
                        // 由 IP 反查主機名稱
                        IPHostEntry host = Dns.GetHostEntry(ipAddress);
                        hostname = host.HostName;
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"{ex.Message} {ex.StackTrace}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex.Message} {ex.StackTrace}");
                    }
                }
            }
            return (ipAddress, hostname);
        }

        /// <summary>
        /// 取得有效的員工編號 (僅限 secinc/sinotech 網域)
        /// </summary>
        /// <param name="username">使用者帳號 (格式: DOMAIN\\username)</param>
        /// <returns>員工編號 (若非指定網域則回傳空字串)</returns>
        public static string GetValidEmpNo(string username)
        {
            string userId = string.Empty; // 員工編號
            // 檢查帳號格式
            if (username.Contains('\\'))
            {
                // 取得網域名稱 (小寫)
                string domain = GetDomain(username).ToLower();
                // 僅允許 secinc 或 sinotech 網域
                if ((domain == "secinc" || domain == "sinotech") && !string.IsNullOrEmpty(username))
                {
                    // 取出 \\ 後的字串 (即員工編號)
                    userId = username[(username.IndexOf('\\') + 1)..];
                }
            }
            return userId;
        }
    }
}