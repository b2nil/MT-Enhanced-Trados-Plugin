using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Runtime.Serialization.Json;
using BDTranslate.Models;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Globalization;
using MtEnhancedTradosPlugin;

namespace BdtTranslateConnect
{
    internal class BaiduApiConnecter
    {
        private static List<string> supportedLangs;
        private MtEnhancedTradosPlugin.MtTranslationOptions options;
        private string cid;
        private string cst;

        /// <summary>
        /// This class allows connection to the Baidu Translate API
        /// </summary>
        /// <param name="_options"></param>
        internal BaiduApiConnecter(MtEnhancedTradosPlugin.MtTranslationOptions _options)
        {
            this.options = _options;
            this.cid = options.BaiduAppID;
            this.cst = options.BaiduApiKey;

        }

        /// <summary>
        /// Allows static credentials to be updated by the calling program
        /// </summary>
        /// <param name="cid">the AppId obtained from Baidu</param>
        /// <param name="cst">the secret obtained from Baidu</param>
        internal void resetCrd(string cid, string cst)
        {
            this.cst = cst;
            this.cid = cid;
        }

        /// <summary>  
        /// Encrypt to a 32 bit lowercase MD5
        /// </summary>  
        /// <param name="srcString">source string to be encrypted</param>  
        /// <returns>encrypted string</returns>  
        internal static string MD5_encrypt(string srcString)
        {
            string encrypted = "";
            MD5 md5 = MD5.Create();
            // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　  
            byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(srcString));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得  
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用32进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符  
                encrypted = encrypted + s[i].ToString("x");
            }
            return encrypted;
        }

        /// <summary>
        /// translates the text input
        /// </summary>
        /// <param name="sourceLang"></param>
        /// <param name="targetLang"></param>
        /// <param name="textToTranslate"></param>
        /// <returns>translate results</returns>
        internal string Translate(string textToTranslate, string sourceLang, string targetLang)
        {

            //convert SDL Trados Studio language codes to Baidu supported language codes
            string sourceLc = convertLangCode(sourceLang);
            string targetLc = convertLangCode(targetLang);

            //url encode input, Baidu requires the query text to be UTF8 encoded
            string formattedSourceText = HttpUtility.UrlEncode(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(textToTranslate)));
            //Baidu need a MD5 encrypted sign using (appid + query + str(salt) + secretKey)
            //Create a salt using the system datetime milliseconds
            string salt = System.DateTime.Now.Millisecond.ToString();
            //sign = (appid + query + str(salt) + secretKey) encrypted in MD5
            string srcForEncrypt = this.cid + textToTranslate + salt + this.cst;
            string sign = MD5_encrypt(srcForEncrypt);

            //Create the uri to connect with Baidu Translate API
            string uri = string.Format("http://api.fanyi.baidu.com/api/trans/vip/translate?q={0}&from={1}&to={2}&appid={3}&salt={4}&sign={5}", formattedSourceText, sourceLc, targetLc, this.cid, salt, sign);


            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
                Stream st = response.GetResponseStream();
                GZipStream temp = null;
                StreamReader stReader;
                if (response.ContentEncoding.ToLower().Contains("gzip"))
                {
                    temp = new GZipStream(st, CompressionMode.Decompress, true);
                    stReader = new StreamReader(temp, Encoding.Default);
                }
                else
                {
                    stReader = new StreamReader(st, Encoding.Default);
                }

                string text = stReader.ReadToEnd();
                stReader.Close();

                if (temp != null)
                    temp.Close();
                st.Close();

                //parsing the return json results
                BaiduResult r = JsonGet(text);
                if (r != null && r.trans_result != null && r.trans_result.Length > 0)
                    return r.trans_result[0].dst;
                else
                    return "";
            }
            catch (WebException e)
            {
                string errorResponse = ProcessWebException(e, PluginResources.BtApiErrorMessage);
                bool requestTimeout = errorResponse.Contains("52001");
                bool systemError = errorResponse.Contains("52002");
                bool outOfMoney = errorResponse.Contains("54004");
                if (requestTimeout | systemError)
                {
                    string x = Translate(textToTranslate, sourceLang, targetLang);
                }
                else if (outOfMoney)
                    throw new Exception(PluginResources.BtApiOutOfMoneyErrorMessage);
                else
                    throw new Exception(errorResponse);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }

            return "";
        }

        /// <summary>
        /// Checks of lang pair is supported by Baidu Translate
        /// </summary>
        /// <param name="sourceLang"></param>
        /// <param name="targetLang"></param>
        /// <returns></returns>
        internal bool isSupportedLangPair(string sourceLang, string targetLang)
        {
            //convert to language codes in the format supported by Baidu
            string source = convertLangCode(sourceLang);
            string target = convertLangCode(targetLang);


            bool sourceSupported = false;
            bool targetSupported = false;

            //check to see if both the source and target languages are supported
            supportedLangs = getSupportedLangs();
            foreach (string lang in supportedLangs)
            {
                if (lang.Equals(source)) sourceSupported = true;
                if (lang.Equals(target)) targetSupported = true;
            }

            if (sourceSupported && targetSupported) return true; //if both are supported return true

            //otherwise return false
            return false;

        }

        //This is a manually created language codes supported by Baidu from their API documentation. Not all are included in this list.
        private List<string> getSupportedLangs()
        {
            return new List<string> { "zh", "en", "jp", "kor", "fra", "spa", "th", "ara", "ru", "de", "cht" };
        }

        private string ProcessWebException(WebException e, string message)
        {
            Console.WriteLine("{0}: {1}", message, e.ToString());

            // Obtain detailed error information
            string strResponse = string.Empty;
            using (HttpWebResponse response = (HttpWebResponse)e.Response)
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(responseStream, System.Text.Encoding.ASCII))
                    {
                        strResponse = sr.ReadToEnd();
                    }
                }
            }
            return String.Format("Http status code={0}, error message={1}", e.Status, strResponse);
        }

        private string convertLangCode(string languageCode)
        {
            CultureInfo ci = new CultureInfo(languageCode); //construct a CultureInfo object with the language code

            //deal with chinese. Baidu Translate has different language strings.
            if (ci.Name == "zh-TW") return "cht";
            if (ci.Name == "zh-CN") return "zh";
            if (ci.Name == "en-US") return "en";
            if (ci.Name == "en-UK") return "en";

            //otherwise, return the two-letter code
            return ci.TwoLetterISOLanguageName;

        }


        //Parsing json results to text results
        private static BaiduResult JsonGet(string jsonString)
        {
            if (jsonString.Length > 0)
            {
                var ms = new MemoryStream(Encoding.Default.GetBytes(jsonString));
                return (BaiduResult)new DataContractJsonSerializer(typeof(BaiduResult)).ReadObject(ms);
            }
            return null;
        }

    }
}

namespace BDTranslate.Models
{
    public class BaiduResult
    {
        [DataMember(Order = 0, IsRequired = true)]
        public string from;
        [DataMember(Order = 1)]
        public string to;
        public class Trans_result
        {
            public string src;
            public string dst;
        }
        [DataMember(Order = 2)]
        public Trans_result[] trans_result;
    }
}