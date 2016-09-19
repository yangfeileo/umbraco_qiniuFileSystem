
namespace FileSystemProvider.QiNiu
{
    using Qiniu.FileOp;
    using Qiniu.IO;
    using Qiniu.RPC;
    using Qiniu.RS;
    using Qiniu.RSF;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Umbraco.Core;
    using Umbraco.Core.IO;
    using Umbraco.Core.Logging;

    public class QiNiuFileSystem : IFileSystem
    {
        //七牛云 ACCESS_KEY SECRET_KEY
        private readonly string ACCESS_KEY = "jbjM-TpWWCB03IJT9ntq7TnNkvZ-KN14D0PlENi-";
        private readonly string SECRET_KEY = "2fmZHfmq1QZL7lLbI5jrFLpA9-D34xhxJsnO-9iw";
        private readonly string bucket = "leoyoung-bucket";

        private readonly string _rootPath;
        private readonly string _rootUrl;

        public QiNiuFileSystem()
        {
            Qiniu.Conf.Config.ACCESS_KEY = ACCESS_KEY;
            Qiniu.Conf.Config.SECRET_KEY = SECRET_KEY;

            //_rootPath = "\\ocigvr9dz.bkt.clouddn.com";
            _rootUrl = "http://ocigvr9dz.bkt.clouddn.com";
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            //var fullPath = GetFullPath(path);

            var exists = FileExists(path);
            if (exists && overrideIfExists == false)
            {
                throw new InvalidOperationException(string.Format("A file at path '{0}' already exists", path));
            }

            //var _path = Path.GetDirectoryName(fullPath);

            //var CreatedDirectory = Directory.CreateDirectory(_path); // ensure it exists

            //if (stream.CanSeek)
            //{
            //    stream.Seek(0, 0);
            //}
            try
            {
                UpLoadToQiNiu(stream, path);
            }
            catch (Exception ex)
            {
                LogHelper.Error<QiNiuFileSystem>($"Unable to upload file at {path}", ex);
            }
        }

        public void AddFile(string path, Stream stream)
        {
            this.AddFile(path, stream, true);
        }

        public void UpLoadToQiNiu(Stream stream, string path)
        {
            IOClient target = new IOClient();
            PutExtra extra = new PutExtra();

            //普通上传,只需要设置上传的空间名就可以了,第二个参数可以设定token过期时间
            PutPolicy put = new PutPolicy(bucket, 3600);

            //调用Token()方法生成上传的Token
            string upToken = put.Token();

            //上传文件的路径
            //String filePath = "/.../...";

            var _path = EnsureUrlSeparatorChar(path);

            //上传 将umbraco的path作为key传入
            PutRet ret = target.Put(upToken, _path, stream, extra);

            LogHelper.Info<PutRet>("上传至七牛云" + (ret.OK ? "成功" : "失败"));

            if (ret.OK)
            {

            }
            else
            {
                throw new Exception("上传失败");
            }
        }

        public void DeleteDirectory(string path)
        {
            this.DeleteDirectory(path, true);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            path = this.FixPath(path);

            if (!this.DirectoryExists(path))
            {
                return;
            }

            //处理path中反斜杠
            path = EnsureUrlSeparatorChar(path);
            path = path.TrimStart('/');
            var _prefix = path;//处理path 得到前缀

            RSFClient rsf = new RSFClient(bucket);
            rsf.Prefix = _prefix;
            rsf.Limit = 100;
            List<DumpItem> items;
            items = rsf.Next();

            if (items != null)
            {
                foreach (var item in items)
                {
                    //实例化一个RSClient对象，用于操作BucketManager里面的方法
                    RSClient client = new RSClient();
                    CallRet ret = client.Delete(new EntryPath(bucket, item.Key));

                    if (ret.OK)
                    {
                        LogHelper.Info<PutRet>("七牛删除 成功");
                    }
                    else
                    {
                        LogHelper.Info<PutRet>("七牛删除 失败");
                    }
                }
            }

        }

        public void DeleteFile(string path)
        {
            //要测试的空间和key，并且这个key在你空间中存在

            //实例化一个RSClient对象，用于操作BucketManager里面的方法
            RSClient client = new RSClient();
            CallRet ret = client.Delete(new EntryPath(bucket, path));

            if (ret.OK)
            {
                LogHelper.Info<PutRet>("七牛删除 OK");
            }
            else
            {
                LogHelper.Info<PutRet>("七牛删除 失败");
            }
        }

        //TODO：未完成
        public bool DirectoryExists(string path)
        {
            //处理path中反斜杠
            path = EnsureUrlSeparatorChar(path);
            path = path.TrimStart('/');

            //判断这个前缀在七牛云中能否找到对应文件
            RSFClient rsf = new RSFClient(bucket);
            rsf.Prefix = path;
            rsf.Limit = 100;
            List<DumpItem> items;
            items = rsf.Next();
            if (items == null)
            {
                return false;
            }

            return items.Count > 0;
        }

        public bool FileExists(string path)
        {
            path = FixPath(path);
            return Stat(bucket, path);
        }

        public bool Stat(string bucket, string key)
        {
            key = HttpUtility.UrlDecode(key);

            //处理key的格式
            if (key.StartsWith(_rootUrl + "/"))
            {
                key = key.Replace(_rootUrl + "/", "");
            }

            if (key.StartsWith("/"))
            {
                key = key.TrimStart('/');
            }

            //实例化一个RSClient对象，用于操作BucketManager里面的方法
            RSClient client = new RSClient();
            //调用Stat方法获取文件的信息
            Entry entry = client.Stat(new EntryPath(bucket, key));

            return entry.OK == true ? true : false;
        }


        public DateTimeOffset GetCreated(string path)
        {
            return new DateTimeOffset();
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            List<string> list = new List<string>();

            RSFClient rsf = new RSFClient(bucket);
            rsf.Prefix = "";
            rsf.Limit = 100;
            List<DumpItem> items;
            items = rsf.Next();
            if (items != null)
            {
                foreach (var item in items)
                {
                    var preifx = item.Key.Split('/')[0];
                    list.Add(preifx);
                }
            }

            return list.Where(z => true);
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return this.GetFiles(path, "*.*");
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            List<string> list = new List<string>();

            RSFClient rsf = new RSFClient(bucket);
            rsf.Prefix = "";
            rsf.Limit = 100;
            List<DumpItem> items;
            items = rsf.Next();
            return items.Where(z => true).Select(z => z.Key);

        }

        public string GetFullPath(string path)
        {
            return ResolveUrl(path, false);
        }

        private string ResolveUrl(string path, bool relative)
        {
            // First create the full url
            string fixedPath = this.FixPath(path);
            Uri url = new Uri(new Uri(this._rootUrl, UriKind.Absolute), fixedPath);

            if (!relative)
            {
                return url.AbsoluteUri;
            }

            //if (this.UseDefaultRoute)
            //{
            //    return $"/{Constants.DefaultMediaRoute}/{fixedPath}";
            //}

            return $"{fixedPath}";
        }

        private string FixPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            //if (path.StartsWith(Delimiter))
            //{
            //    path = path.Substring(1);
            //}

            // Strip root url
            if (path.StartsWith(this._rootUrl, StringComparison.InvariantCultureIgnoreCase))
            {
                path = path.Substring(this._rootUrl.Length);
            }

            // Strip default route
            //if (path.StartsWith(Constants.DefaultMediaRoute, StringComparison.InvariantCultureIgnoreCase))
            //{
            //    path = path.Substring(Constants.DefaultMediaRoute.Length);
            //}

            // Strip container Prefix
            if (path.StartsWith(bucket, StringComparison.InvariantCultureIgnoreCase))
            {
                path = path.Substring(bucket.Length);
            }

            //if (path.StartsWith(Delimiter))
            //{
            //    path = path.Substring(1);
            //}

            return path;
        }

        // 未实现
        public DateTimeOffset GetLastModified(string path)
        {
            return new DateTimeOffset();
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            return this.ResolveUrl(fullPathOrUrl, true);
        }

        public string GetUrl(string path)
        {
            path = EnsureUrlSeparatorChar(path);
            //path = path.Replace("\\", "%5C");
            if (!path.StartsWith(_rootUrl))
            {
                return _rootUrl + "/" + path;
            }
            return path;
        }

        public Stream OpenFile(string path)
        {
            //path = GetUrl(path);

            WebRequest myrequest = WebRequest.Create(path);
            WebResponse myresponse = myrequest.GetResponse();
            Stream imgstream = myresponse.GetResponseStream();

            if (imgstream.CanSeek)
            {
                imgstream.Seek(0, SeekOrigin.Begin);
            }

            return imgstream;
        }


        #region Helper Methods

        protected virtual void EnsureDirectory(string path)
        {
            path = GetFullPath(path);
            Directory.CreateDirectory(path);
        }

        protected string EnsureTrailingSeparator(string path)
        {
            return path.EnsureEndsWith(Path.DirectorySeparatorChar);
        }

        protected string EnsureDirectorySeparatorChar(string path)
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            return path;
        }

        protected string EnsureUrlSeparatorChar(string path)
        {
            path = path.Replace('\\', '/');
            return path;
        }

        #endregion
    }

    public static class StringExtensions
    {
        public static string EnsureEndsWith(this string input, char value)
        {
            return input.EndsWith(value.ToString(CultureInfo.InvariantCulture)) ? input : input + value;
        }

        public static bool PathStartsWith(string path, string root, char separator)
        {
            // either it is identical to root,
            // or it is root + separator + anything

            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            if (path.Length == root.Length)
            {
                return true;
            }

            if (path.Length < root.Length)
            {
                return false;
            }

            var ss = path[root.Length];
            if (path[root.Length] == separator)
            {
                return true;
            }
            else
            {
                return true;
            }
        }
    }
}
