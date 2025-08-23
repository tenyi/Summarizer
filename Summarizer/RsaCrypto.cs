using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sinotech.Mis.Extensions.Configuration
{
    /// <summary>
    /// 使用RSA實現加解密
    /// </summary>
    public static class RsaCrypto
    {
        /// <summary>
        /// 使用RSA實現解密
        /// </summary>
        /// <param name="data">解密資料</param>
        /// <returns></returns>
        /// <returns></returns>
        public static string RsaDecrypt(string encryptText)
        {
            // C#預設只能使用[私鑰]進行解密(想使用[私鑰加密]可使用第三方元件BouncyCastle來實現)
            string privateKey = @"<RSAKeyValue><Modulus>0e3VQagf8UBiqTdlIXZX0Lt/4e6xEqsd2tUul7sJ9HoIcEb71U4T7Lt1zbkyR0k+C/rGY5e/xNGQ+bMByLqDZmRuMfAldONmmkDJBPIQHXklIypEz1vzPEnYjTbUv/EPuWJ7XTYMULJ8kwtW7euORPhZQ6fBavobNFfOqBg0wDyfVNl2TirtJMSuLJItlpfAkJ/MFc5wyvgmmU+ej11pdHaG8rLvWtAj0isSQ3zZ7jiJU22iRfoBXe9GlVXYmu+O9zaZXjflVpEtwTbIoiLROXxvcItj6ks58vqXtntN26QVxOw/aPrvrZ3+XQH+Q7JFnfCBhyRRf1ObVCZ64JW9granNG6j/VYcRdw6tywFFuzbGbVJCpzf7MHI+Br6n3lh/bKOpdDyaFeiWsutc4HT5HCQjEN+PF0ODoJsLNDnmEqCK25ej2YPWH+2GK6W/TXhaU3bPhJe2IMfhlSS+Ut9hfYyIYJuu8aleC0RipvOoQgM8kDGrYuuUNSBVIGTyPbHFLFY1kdt7lgpEq/HN/Iyps72+TFETyAhk36jCbeSTatEqGNVsOHGh96U1PRFcz8k7OAOZaUtWOeT4Vf8hu5axsAVGxWTKNqDCKcd6bokoN5PQ48rnOn01Ap9+FvBib7uOEIWhDptg1l0Jjc26D7FciOoxYdRTqbw7PKp9cQCO60=</Modulus><Exponent>AQAB</Exponent><P>64OWxM0u5PAAtj2kmxZw+nFR/WEfJoh7LYgDPTscmTy+5Ijs90z8FVxDItH+nSYoR9vOBvjnefun/47fgl252BJvd3rTk2wG3iC8epSAKe1oKTbHq+Ziuuzu5nrW/e9kY53UqJB3aQ6txJXF4a5dh+5y/N5W7xrimfyijZ6SadIJu9Ds+ICzV/ixAB5Gqd7DaXa9v5NcRpPiqU5RasBEEwj4PWTqso3njCfnntIBe7WiUrf9QBdIx2z8bPjTBDN3zGhO5zTNp33wuypPTQ+YUA4xZTPsQmzI36adVN4svFb6H6AP9HauLEAEeLFiNKbASCb1q1XZHeGQjz5mMgYDbw==</P><Q>5DCE8ZgHyYBYifJ0xtquQxDvlGv1DyOlSlgrR/6VC6ktumaDRbkio+TgXPwgTZkhGWN254lz6Jj3FyXiaffPZdk/EoVgEc2h5/vK2hR0WVBDvqG4gBJoLKNvOrZIttucKACcmtjAjQUw+dNO4Rpha6RSMhI5lPgA6BYrRwyyvyDZAcSb1yqy6L4OEpeZQTAvz2D2CIyEYKCd7DskIOzVFVsSLsgbG68zseDyl3EptFNohVOk0U8oyKyA41U2n+KYrXYqhHHa5ql7MblL+RbA02lYtU6VONyTA4SNvi76BBltWObg06TP2ISbp6vkZW/pw+GUajxtS9tDaI13Wny0ow==</Q><DP>3dchmVc8qOF5ijAecQd+fClp9aYaVY2RmZ3Yj0Cy+5TAqzxfGcMmYGh7xLzRfO2IW8esFd03DSwDXzIRTxdCC0myPXUw/lhvT6S+ZSikYNoDl27GPiHRNgogLnHfOWrZwQvWWMFb+VWeUOJTdvVrnLL7FT3J3YgRksLpy0Rm3c6+5G80CBAS3vaeo2HhZfkzUJBMw7vm24RJqPjgsBFTDisQyaPZfz8zsE2WW3+tbf31XL16i3hjUZZTX8Ix4m8olR5b1GVkojR7IZIFCK1750y3MoDqNteJj0G+SUbOFMpI6Kk43ik8fun+TEGqI7Y+wLgSCMXgscjbBlLcqJoJeQ==</DP><DQ>t9IGJoDsNPy5VlcEj3R2UPyBozTccteqPZuNMi/5bS1Z8wDF2xLqvtCPlRheBWTH7yXbEWX/A27GDdWs8OR6JHe/gXIEFBNsy+5gWRGFMLWh/R1V/YXWea8m2UsihHug7fCgN3VIl9GIxJfewSN5OzdiJ4fa7xBh2pSRRGkMTT9u0SfKyqhQ+4Cu4XdSM9tXiF15lSVtNlHtJfH2hreCZ/O3UAxPGhwnLrIherHkgKl4V38sFJkKJ64fHgL1QBNQHtxEH/F//7Sj00iL1RrpNuV0Won1V5sIqxrK/FvneAnrtmQ21Qe9j5Qzt/yWvshKrks4PeRzv1ngkYb0kOZItQ==</DQ><InverseQ>ELHhxQBxXvgMwesTtoIkqk8w9wa/zCXodGp9I1lR4AMOhFi2c56H84k7u+FisNE/Ftu+NOJ68Koi/noSiRCG5t+c6RNeO/bvjSgu9ozYC5+uUTfJvmPRJP/CpkThrMMllopAImugYRWXDYqcwK+3J015CK0Z7osxnr+sQaDebXLxDK5xqI3NDlP9HlrMXKFtK06yH/8UJVJ0Ny9eoL8EXao6euCvlfwhf4Ji4W3EqK/rs+XAegXAbhSJJaeWq3jiZr1yjDny8fFVP+flKBPX2pgWnJXf/wEYBx0fNwCp0qsvm4LH4QwOCfslOxariiwMfT6fgtvteikzsekDdqFT+Q==</InverseQ><D>IOGIqo0D28zC51BG5dPcc1Q69o9latAkj/ceIiPorkNC+RsVLNba5hSCoiNkzaeaMVQpKMZHAjP06jdwixkzpaELZYUAyOspUfXdxomHnqYv++8N8hCr64CBi7TP4/SFCvty6SmjCiy6uGlpR0DC+uiPSrqG4BOmmS87rjaEZKvaJPcewaWVmVG5GkAXJeRFBCITXEMGhbQSj6bZ5giykMxT3MXMGcRKKAwZAzsWA2sVj1y3sxAykJz+yDs2/yWQlgYWRZyprkJ1ETNcf5DGhqOH7O/YSYVY4UcA5lMv4bw0sLB0SiQr0FzTz4uVYNMWdmOKVjmJDlFxHeAdFPo/QWZrIy8Qnhr7IcdbHlXdhMkROGFudCy7/z6AWLq9CkNi32XcZQ7zTqChB4E5zM0RLCxdBbg474cr6BjOFM8SVYsaMxut798nWSvP8uVMFE6uuOa2fToOOAlqhdhudh9VfPIxicSFP502gPrFJhhFBKFCdba6e7eKT7g+mZeRn7tohh5xM7wTWy+Y96G9arVLkUBtS3jpfOgx8elS2VN6xhvNxNBGrW0U9E+9PRvvzYuYSXIJxMpruCaKhApz+Snd01LyEaZE5d+P5d+BbHJcnK2WRnUQW7yc3OgD3yWJI4xK9p3rgCPC1xTpnS4NDnV17t9MeKMbUdYMR/FbQh3HOj0=</D></RSAKeyValue>";

            //建立RSA物件並載入[私鑰]
            RSACryptoServiceProvider rsaPrivate = new RSACryptoServiceProvider();
            rsaPrivate.FromXmlString(privateKey);
            string result = RsaDecrypt(rsaPrivate, encryptText);
            return result;
        }

        private const int KeyLength = 2048;
        /// <summary>
        /// 使用RSA實現解密
        /// </summary>
        /// <param name="rsaPrivate"></param>
        /// <param name="encryptText"></param>
        /// <returns></returns>
        public static string RsaDecrypt(RSACryptoServiceProvider rsaPrivate, string encryptText)
        {
            string result = encryptText;
            bool useOAEP = true; // 使用 OAEP填充模式
            try
            {
                result = RsaDecrypt(rsaPrivate, encryptText, useOAEP);
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine($"OAEP模式解密失敗: {ex.Message}");
                try
                {
                    useOAEP = false; // 使用 PKCS#1 v1.5填充模式 再試一次
                    result = RsaDecrypt(rsaPrivate, encryptText, useOAEP);
                }
                catch (CryptographicException ex2)
                {
                    Console.WriteLine($"PKCS#1 v1.5模式解密失敗: {ex2.Message}");
                }
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.StackTrace}");
            }
            return result;
        }

        /// <summary>
        /// 使用RSA實現解密
        /// </summary>
        /// <param name="rsaPrivate"></param>
        /// <param name="encryptText"></param>
        /// <param name="useOAEP">true使用OAEP，false 使用 PKCS#1 v1.5</param>
        /// <returns></returns>
        private static string RsaDecrypt(RSACryptoServiceProvider rsaPrivate, string encryptText, bool useOAEP)
        {
            string result = encryptText;
            //Create a UnicodeEncoder to convert between byte array and string.
            if (!string.IsNullOrWhiteSpace(encryptText))
            {
                byte[] dataByteArray = Convert.FromBase64String(encryptText); //使用Base64將string轉換為byte  
                                                                              //對資料進行解密
                byte[] privateValue = rsaPrivate.Decrypt(dataByteArray, useOAEP);
                if (privateValue.Length != 0)
                {
                    UnicodeEncoding ByteConverter = new UnicodeEncoding();
                    result = ByteConverter.GetString(privateValue);
                }
            }
            return result;
        }

        /// <summary>
        /// 使用RSA實現解密
        /// </summary>
        /// <param name="privateKeyPath">私鑰路徑</param>
        /// <param name="data">解密資料</param>
        /// <returns></returns>
        /// <returns></returns>
        public static string RsaDecrypt(string privateKeyPath, string encryptText)
        {
            string result = encryptText;
            // C#預設只能使用[私鑰]進行解密(想使用[私鑰加密]可使用第三方元件BouncyCastle來實現)
            string privateKey = File.ReadAllText(privateKeyPath);
            //建立RSA物件並載入[私鑰]
            RSACryptoServiceProvider rsaPrivate = new RSACryptoServiceProvider(KeyLength);
            try
            {
                rsaPrivate.FromXmlString(privateKey);
                result = RsaDecrypt(rsaPrivate, encryptText);
            }
            catch (CryptographicException ex)
            {
                Console.WriteLine($"{ex.Message} {ex.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// 使用RSA實現加密
        /// </summary>
        /// <param name="publicKeyPath"></param>
        /// <param name="inputText"></param>
        /// <returns></returns>
        public static string RsaEncrypt(string publicKeyPath, string inputText)
        {
            string result = inputText;
            //C#預設只能使用[公鑰]進行加密(想使用[公鑰解密]可使用第三方元件BouncyCastle來實現)
            try
            {
                //建立RSA物件並載入[公鑰]
                string publicKey = File.ReadAllText(publicKeyPath);
                RSACryptoServiceProvider rsaPublic = new RSACryptoServiceProvider(KeyLength);

                rsaPublic.FromXmlString(publicKey);
                //Create a UnicodeEncoder to convert between byte array and string.
                UnicodeEncoding ByteConverter = new UnicodeEncoding();
                byte[] dataToEncrypt = ByteConverter.GetBytes(inputText);

                //對資料進行加密
                byte[] publicValue = rsaPublic.Encrypt(dataToEncrypt, true);
                result = Convert.ToBase64String(publicValue);//使用Base64將byte轉換為string
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return result;
        }
    }
}
