using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MaFi.WebShareCz.ApiClient.Entities.Internals;
using System.IO;
using System.Threading;
using MaFi.WebShareCz.ApiClient.Entities;
using MaFi.WebShareCz.ApiClient.Http;
using System.Security.Authentication;
using MaFi.WebShareCz.ApiClient.Security;
using System.Net;

namespace MaFi.WebShareCz.ApiClient
{
    public class WsApiClient
    {
        private static readonly Uri WEBSHARE_URI = new Uri("https://webshare.cz/");
        private static readonly Uri API_URI = new Uri(WEBSHARE_URI, "api/");
        private static readonly Uri API_SALT_URI = new Uri(API_URI, "salt/");
        private static readonly Uri API_LOGIN_URI = new Uri(API_URI, "login/");
        private static readonly Uri API_LOGOUT_URI = new Uri(API_URI, "logout/");
        private static readonly Uri API_FILES_URI = new Uri(API_URI, "files/");
        private static readonly Uri API_FOLDER_URI = new Uri(API_URI, "folder/");
        private static readonly Uri API_FILE_LINK_URI = new Uri(API_URI, "file_link/");
        private static readonly Uri API_UPLOAD_URL_URI = new Uri(API_URI, "upload_url/");
        private static readonly Uri API_REMOVE_FILE_URI = new Uri(API_URI, "remove_file/");
        private static readonly Uri API_CREATE_FOLDER_URI = new Uri(API_URI, "create_folder/");
        private static readonly Uri API_RENAME_FILE_URI = new Uri(API_URI, "rename_file/");
        private static readonly Uri API_MOVE_FILE_URI = new Uri(API_URI, "move_file/");

        internal readonly CreatedFileResolver _createdFileResolver;
        private readonly WsHttpClient _httpClient = new WsHttpClient(WEBSHARE_URI);
        private readonly Guid _deviceUuid;
        private LoginInfo _loginInfo = null;
        private WsFolder _privateRootFolder = null;
        private WsFolder _publicRootFolder = null;
        private Uri _uploadFileUrl = null;

        private sealed class LoginInfo
        {
            public LoginInfo(string userName, string userPasswordHash, ISecretProvider secretProvider, ISecretPersistor secretPersistor)
            {
                UserName = userName;
                UserPasswordHash = userPasswordHash;
                SecretProvider = secretProvider;
                SecretPersistor = secretPersistor;
            }

            public string UserName { get; }
            public string UserPasswordHash { get; }
            public string LoginToken { get; set; }
            public ISecretProvider SecretProvider { get; }
            public ISecretPersistor SecretPersistor { get; }
        }

        public WsApiClient(Guid deviceUuid)
        {
            _deviceUuid = deviceUuid;
            _createdFileResolver = new CreatedFileResolver(this);
        }

        public bool IsLoggedIn => _loginInfo != null;

        public string UserName => _loginInfo?.UserName ?? "[unknown user]";

        public WsFolder PrivateRootFolder => _loginInfo == null ? null : _privateRootFolder;

        public WsFolder PublicRootFolder => _loginInfo == null ? null : _publicRootFolder;

        public async Task<bool> Login(string userName, ISecretProvider secretProvider, ISecretPersistor secretPersistor = null)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException($"{nameof(userName)} not specified.", nameof(userName));
            if (secretProvider == null)
                throw new ArgumentNullException(nameof(secretProvider));
            if (IsLoggedIn)
                throw new InvalidOperationException("The client is already logged in.");

            if (secretProvider.TryGetUserPasswordHash(out string userPasswordHash) == false)
            {
                string userPassword = await secretProvider.GetPassword();
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                    new KeyValuePair<string, string>("username_or_email", userName)
                });
                SaltResult saltResult = await _httpClient.PostFormData<SaltResult>(API_SALT_URI, formContent);
                if (CheckResultStatus(saltResult))
                    userPasswordHash = BCrypt.HashPassword(userPassword, saltResult.Salt);
                else
                    return false;
            }

            LoginInfo loginInfo = new LoginInfo(userName, userPasswordHash, secretProvider, secretPersistor);
            if (await TryLogin(loginInfo))
            {
                _loginInfo = loginInfo;
                _privateRootFolder = WsFolder.CreateRootFolder(this, true);
                _publicRootFolder = WsFolder.CreateRootFolder(this, false);
                secretPersistor?.SaveUserPasswordHash(userPasswordHash);
                return true;
            }
            return false;
        }

        public async Task<bool> Logout()
        {
            CheckConnected();
            FormUrlEncodedContent formContent = CreateFormContent();
            try
            {
                await _createdFileResolver.Clear();
                return (await _httpClient.PostFormData<Result>(API_LOGOUT_URI, formContent)).Status == ResultStatus.OK;
            }
            finally
            {
                _uploadFileUrl = null;
                _privateRootFolder = null;
                _publicRootFolder = null;
                _loginInfo = null;
            }
        }

        public async Task<WsItemsReader> GetFolderItems(WsFolderPath folderPath)
        {
            WsItemsReaderEngine filesResult = await GetItems(folderPath, false, true);
            return new WsItemsReader(filesResult);
        }

        public async Task<WsFilesReader> GetFolderAllFilesRecursive(WsFolderPath folderPath, int depth)
        {
            WsItemsReaderEngine filesResult = await GetItems(folderPath, false, true);
            return new WsFilesReader(filesResult, depth);
        }

        public async Task<WsFile> FindFile(WsFilePath filePath)
        {
            using (WsItemsReaderEngine filesResult = await GetItem(filePath, true))
            {
                return filesResult.GetItems().OfType<WsFile>().Where(f => f.PathInfo.Equals(filePath)).SingleOrDefault();
            };
        }

        public async Task<WsFile> GetFile(WsFilePath filePath)
        {
            WsFile file = await FindFile(filePath);
            if (file == null)
                throw new FileNotFoundException("Required file not found on server.", filePath.ToString());
            return file;
        }

        public async Task<WsFolder> FindFolder(WsFolderPath folderPath)
        {
            if (folderPath.FullPath == "/")
                return folderPath.IsPrivate ? PrivateRootFolder : PublicRootFolder;
            using (WsItemsReaderEngine itemsResult = await GetItems(folderPath.Parent, false, false))
            {
                foreach (WsItem item in itemsResult.GetItems())
                {
                    if (item is WsFolder folder)
                    {
                        if (folder.PathInfo.Equals(folderPath))
                            return folder;
                    }
                    else
                        break;
                }
            };
            return null;
        }

        public async Task<WsFolder> GetFolder(WsFolderPath folderPath)
        {
            WsFolder folder = await FindFolder(folderPath);
            if (folder == null)
                throw new DirectoryNotFoundException($"Required folder {folderPath} not found on server.");
            return folder;
        }

        public Task<WsFile> UploadFile(Stream sourceStream, long sourceSize, WsFilePath targetFilePath, CancellationToken cancellationToken, IProgress<int> progress = null)
        {
            return UploadFile(sourceStream, sourceSize, targetFilePath, true, cancellationToken, progress);
        }

        public async Task<WsFolder> CreateFolder(string folderPath, bool isPrivate)
        {
            CheckConnected();
            CreateFolderResult newFolderResult = await PostFormDataWithLoginRetry(() =>
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                    new KeyValuePair<string, string>("path", folderPath),
                    new KeyValuePair<string, string>("private", isPrivate ? "1" : "0")
                });
                return _httpClient.PostFormData<CreateFolderResult>(API_CREATE_FOLDER_URI, formContent);
            });
            return new WsFolder(newFolderResult, isPrivate, this);
        }

        internal async Task DownloadFile(WsFile sourceFile, Stream targetStream, CancellationToken cancellationToken, IProgress<int> progress)
        {
            if (sourceFile == null)
                throw new ArgumentNullException(nameof(sourceFile));
            if (targetStream == null)
                throw new ArgumentNullException(nameof(targetStream));
            if (targetStream.CanWrite == false)
                throw new NotSupportedException("The target stream does not support writing.");
            if (cancellationToken == null)
                throw new ArgumentNullException(nameof(cancellationToken));

            progress?.Report(0);

            using (HttpResponseMessage response = await DownloadFile(sourceFile, cancellationToken))
            {
                long fileSize = response.Content.Headers.ContentLength ?? sourceFile.Size;
                using (TransportStream sourceTrasportStream = new TransportStream(await response.Content.ReadAsStreamAsync(), fileSize, progress))
                {
                    await sourceTrasportStream.CopyToAsync(targetStream, CalculateBuffer(fileSize), cancellationToken);
                    progress?.Report(100);
                }
            }
        }

        internal async Task<WsFile> UploadFile(Stream sourceStream, long sourceSize, WsFilePath targetFilePath, bool ensureDeleteIfNeed, CancellationToken cancellationToken, IProgress<int> progress)
        {
            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));
            if (sourceStream.CanRead == false)
                throw new NotSupportedException("The source stream does not support reading.");
            if (cancellationToken == null)
                throw new ArgumentNullException(nameof(cancellationToken));
            CheckConnected();
            progress?.Report(0);

            if (ensureDeleteIfNeed)
            {
                WsFile file = await FindFile(targetFilePath);
                if (file != null)
                    await file.Delete();
            }

            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(WebUtility.UrlEncode(targetFilePath.Name)), "name");
            form.Add(new StringContent(_loginInfo.LoginToken), "wst");
            form.Add(new StringContent(WebUtility.UrlEncode(targetFilePath.Folder.FullPath)), "folder");
            form.Add(new StringContent(targetFilePath.IsPrivate ? "1" : "0"), "private");
            form.Add(new StringContent("0"), "adult");
            form.Add(new StringContent(sourceSize.ToString()), "total");
            //form.Add(new StringContent(""), "offset");

            TransportStream sourceTransportStream = new TransportStream(sourceStream, sourceSize, progress);
            TransportStreamContent fileContent = new TransportStreamContent(sourceTransportStream, targetFilePath.Name, CalculateBuffer(sourceSize));
            form.Add(fileContent);

            UploadResult uploadResult = await _httpClient.PostFormData<UploadResult>(await GetUploadFileUrl(), form, cancellationToken, false, TimeSpan.FromHours(24));
            if (string.IsNullOrWhiteSpace(uploadResult?.Ident))
                throw new IOException($"Server upload error: result={uploadResult.Result}");

            return _createdFileResolver.Add(targetFilePath.Folder, targetFilePath.Name, uploadResult.Ident, sourceSize);
        }

        internal async Task<WsItem> CopyItem(WsItem sourceItem, WsFolder targetFolder, CancellationToken cancellationToken, IProgress<int> progress)
        {
            if (sourceItem == null)
                throw new ArgumentNullException(nameof(sourceItem));
            if (targetFolder == null)
                throw new ArgumentNullException(nameof(targetFolder));
            if (cancellationToken == null)
                throw new ArgumentNullException(nameof(cancellationToken));

            progress?.Report(0);

            if (sourceItem is WsFile sourceFile)
            {
                using (HttpResponseMessage response = await DownloadFile(sourceFile, cancellationToken))
                {
                    long fileSize = response.Content.Headers.ContentLength ?? sourceFile.Size;
                    int bufferSize = CalculateBuffer(fileSize);
                    WsFilePath targetFilePath = new WsFilePath(targetFolder.PathInfo, sourceItem.PathInfoGeneric.Name);
                    using (TransportStream sourceTrasportStream = new TransportStream(await response.Content.ReadAsStreamAsync(), fileSize, null))
                    {
                        return await UploadFile(sourceTrasportStream, fileSize, targetFilePath, cancellationToken, progress);
                    }
                }
            }
            else
                throw new NotSupportedException("Copy folder not supported.");
        }

        internal async Task DeleteItem(WsItem item)
        {
            CheckConnected();

            Task<Result> DeleteFunc()
            {
                FormUrlEncodedContent formContent = this.CreateFormContent(new[] { new KeyValuePair<string, string>("ident", item.Ident) });
                return _httpClient.PostFormData<Result>(API_REMOVE_FILE_URI, formContent);
            }

            if (item is WsFile file)
                await PostFormDataWithNotFoundAndLoginRetry(file, DeleteFunc);
            else
                await PostFormDataWithLoginRetry(DeleteFunc);
            _createdFileResolver.RemoveItem(item);
        }

        internal async Task RenameItem(WsItem item, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentNullException(nameof(newName));
            CheckConnected();

            Task<Result> RenameFunc()
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                    new KeyValuePair<string, string>("ident", item.Ident),
                    new KeyValuePair<string, string>("name", newName)
                });
                return _httpClient.PostFormData<Result>(API_RENAME_FILE_URI, formContent);
            }

            if (item is WsFile file)
            {
                WsFile targetFile = await FindFile(new WsFilePath(file.PathInfo.Folder, newName));
                if (targetFile != null)
                    await targetFile.Delete();
                await PostFormDataWithNotFoundAndLoginRetry(file, RenameFunc);
            }
            else
                await PostFormDataWithLoginRetry(RenameFunc);

            _createdFileResolver.RenameItem(item, newName);
            item.ChangeNameInInstance(newName);
        }

        internal async Task MoveItem(WsItem sourceItem, WsFolder targetFolder)
        {
            if (sourceItem == null)
                throw new ArgumentNullException(nameof(sourceItem));
            if (targetFolder == null)
                throw new ArgumentNullException(nameof(targetFolder));
            if (sourceItem.PathInfoGeneric.Folder.Equals(targetFolder.PathInfo))
                throw new ArgumentException($"{nameof(sourceItem)} and {nameof(targetFolder)} are equal folder.");
            CheckConnected();

            Task<Result> MoveFunc()
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                    new KeyValuePair<string, string>("src", sourceItem.PathInfoGeneric.FullPath),
                    new KeyValuePair<string, string>("src_private", sourceItem.PathInfoGeneric.IsPrivate ? "1" : "0"),
                    new KeyValuePair<string, string>("dest", targetFolder.PathInfo.FullPath),
                    new KeyValuePair<string, string>("dest_private", targetFolder.PathInfo.IsPrivate ? "1" : "0"),

                });
                return _httpClient.PostFormData<Result>(API_MOVE_FILE_URI, formContent);
            }

            if (sourceItem is WsFile file)
            {
                WsFile targetFile = await FindFile(new WsFilePath(targetFolder.PathInfo, file.PathInfo.Name));
                if (targetFile != null)
                    await targetFile.Delete();
                await PostFormDataWithNotFoundAndLoginRetry(file, MoveFunc);
            }
            else
                await PostFormDataWithLoginRetry(MoveFunc);

            _createdFileResolver.MoveItem(sourceItem, targetFolder);
            sourceItem.ChangePathInInstance(targetFolder.PathInfo.FullPath + sourceItem.PathInfoGeneric.Name, targetFolder.PathInfo.IsPrivate);
        }

        internal Task<WsItemsReaderEngine> GetFolderItemsWithoutCreatedFilesInProgress(WsFolderPath folderPath, string fileName = "")
        {
            if (string.IsNullOrEmpty(fileName))
                return GetItems(folderPath, false, false);
            return GetItem(new WsFilePath(folderPath, fileName), false);
        }

        internal Task<WsItemsReaderEngine> GetItems(WsFolderPath folderPath, bool onlyOne, bool useCreatedFileResolver)
        {
            return GetItemsOrItem(folderPath, onlyOne, useCreatedFileResolver);
        }
        internal Task<WsItemsReaderEngine> GetItem(WsFilePath filePath, bool useCreatedFileResolver)
        {
            return GetItemsOrItem(filePath, true, useCreatedFileResolver);
        }

        internal async Task<WsFilesPreviewReader> GetFolderFilesPreview(WsFolder folder)
        {
            if (folder == null)
                throw new ArgumentNullException(nameof(folder));
            CheckConnected();

            Task<WsFilesReader> filesReaderTask = folder.GetAllFilesRecursive(0);
            WsFilesPreviewReaderEngine readerEngine = await PostFormDataWithLoginRetry(async () =>
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                            new KeyValuePair<string, string>("ident", folder.Ident),
                            new KeyValuePair<string, string>("limit", "99999999"),
                            //new KeyValuePair<string, string>("offset", "0")
                            });
                HttpResponseMessage response = await _httpClient.PostFormData(API_FOLDER_URI, formContent);
                return await WsFilesPreviewReaderEngine.Create(_httpClient, filesReaderTask, response);
            });
            CheckResultStatus(readerEngine);
            return new WsFilesPreviewReader(readerEngine);
        }

        private async Task<WsItemsReaderEngine> GetItemsOrItem(WsItemPath path, bool onlyOne, bool useCreatedFileResolver)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            CheckConnected();
            string fileSearch = path is WsFilePath ? path.Name : string.Empty;
            WsItemsReaderEngine readerEngine = await PostFormDataWithLoginRetry(async () =>
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                            new KeyValuePair<string, string>("path", path.Folder.FullPath),
                            new KeyValuePair<string, string>("private", path.Folder.IsPrivate ? "1" : "0"),
                            //new KeyValuePair<string, string>("sort_by", "name"),
                            //new KeyValuePair<string, string>("sort_order", "asc"),
                            //new KeyValuePair<string, string>("sort_order", "asc"),
                            new KeyValuePair<string, string>("limit", onlyOne ? "1" : "99999999"),
                            //new KeyValuePair<string, string>("offset", "0")
                            new KeyValuePair<string, string>("search", fileSearch),
                            });
                HttpResponseMessage response = await _httpClient.PostFormData(API_FILES_URI, formContent);
                return await WsItemsReaderEngine.Create(this, response, path.Folder, useCreatedFileResolver);
            });
            CheckResultStatus(readerEngine);
            return readerEngine;
        }

        private async Task<bool> TryLogin(LoginInfo loginInfo)
        {
            FormUrlEncodedContent formContent = CreateFormContent(new[]
            {
                new KeyValuePair<string, string>("username_or_email", loginInfo.UserName),
                new KeyValuePair<string, string>("password", loginInfo.UserPasswordHash),
                new KeyValuePair<string, string>("keep_logged_in", "1")
            });
            LoginResult loginResult = await _httpClient.PostFormData<LoginResult>(API_LOGIN_URI, formContent);
            if (CheckResultStatus(loginResult) && string.IsNullOrWhiteSpace(loginResult.Token) == false)
            {
                loginInfo.LoginToken = loginResult.Token;
                return true;
            }
            return false;
        }

        private async Task<HttpResponseMessage> DownloadFile(WsFile file, CancellationToken cancellationToken)
        {
            FileLinkResult fileLinkResult = await PostFormDataWithNotFoundAndLoginRetry(file, () =>
            {
                FormUrlEncodedContent formContent = CreateFormContent(new[]
                {
                    new KeyValuePair<string, string>("ident", file.Ident),
                    new KeyValuePair<string, string>("password", ""),
                    new KeyValuePair<string, string>("download_type","file_download"),
                    new KeyValuePair<string, string>("device_uuid", _deviceUuid.ToString()),
                    new KeyValuePair<string, string>("device_res_x","1920"),
                    new KeyValuePair<string, string>("device_res_x","1080"),
                    new KeyValuePair<string, string>("force_https", "1")
                });
                return _httpClient.PostFormData<FileLinkResult>(API_FILE_LINK_URI, formContent, cancellationToken);
            });
            return await _httpClient.GetData(new Uri(fileLinkResult.Link), cancellationToken);
        }

        private async Task<Uri> GetUploadFileUrl()
        {
            if (_uploadFileUrl == null)
            {
                UploadUrlResult uploadFileUrlResult = await PostFormDataWithLoginRetry(() =>
                {
                    FormUrlEncodedContent formContent = CreateFormContent();
                    return _httpClient.PostFormData<UploadUrlResult>(API_UPLOAD_URL_URI, formContent);
                });
                _uploadFileUrl = new Uri(uploadFileUrlResult.Url);
            }
            return _uploadFileUrl;
        }

        private async Task<TResult> PostFormDataWithLoginRetry<TResult>(Func<Task<TResult>> postFunc) where TResult : Result
        {
            TResult result = await postFunc();
            if (CheckResultStatus(result) == false)
            {
                if (await TryLogin(_loginInfo))
                {
                    result = await postFunc();
                    if (CheckResultStatus(result) == false)
                        throw new AuthenticationException("Unauthorized access.");
                }
            }
            return result;
        }

        private async Task<TResult> PostFormDataWithNotFoundAndLoginRetry<TResult>(WsFile file, Func<Task<TResult>> postFunc) where TResult : Result
        {
            TResult result;
            if (file.IsReady == false)
            {
                try
                {
                    result = await PostFormDataWithLoginRetry(postFunc);
                }
                catch (FileNotFoundException)
                {
                    await file.WaitForReadyFile();
                    result = await PostFormDataWithLoginRetry(postFunc);
                }
            }
            else
                result = await PostFormDataWithLoginRetry(postFunc);
            return result;
        }

        private FormUrlEncodedContent CreateFormContent(IEnumerable<KeyValuePair<string, string>> formData = null)
        {
            if (formData == null)
                formData = new KeyValuePair<string, string>[0];
            if (_loginInfo?.LoginToken != null)
                formData = formData.Concat(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("wst", _loginInfo.LoginToken) });
            return new FormUrlEncodedContent(formData);
        }

        private void CheckConnected()
        {
            if (_loginInfo == null)
                throw new InvalidOperationException("The client is not logged in.");
        }

        private void CheckFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || filePath.StartsWith("/") == false || filePath.TrimEnd().EndsWith("/"))
                throw new ArgumentException($"Invalid path of file: {filePath ?? "is null"}", nameof(filePath));
        }

        private void CheckFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || folderPath.StartsWith("/") == false)
                throw new ArgumentException($"Invalid path of folder: {folderPath ?? "is null"}", nameof(folderPath));
        }

        private bool CheckResultStatus(Result result)
        {
            if (result.Status != ResultStatus.OK)
            {
                (result as IDisposable)?.Dispose();
                switch (result.ErrorCode)
                {
                    case "LOGIN_FATAL_1":
                    case "SALT_FATAL_1":
                    case "CREATE_FOLDER_FATAL_1":
                    case "FILES_FATAL_1":
                    case "MOVE_FILE_FATAL_1":
                    case "REMOVE_FILE_FATAL_1":
                    case "RENAME_FILE_FATAL_1":
                        return false;
                    case "FILE_INFO_FATAL_1":
                    case "FILE_LINK_FATAL_1":
                    case "FILES_FATAL_2":
                    case "FOLDER_FATAL_1":
                    case "FOLDER_LINK_FATAL_1":
                    case "MOVE_FILE_FATAL_3":
                    case "REMOVE_FILE_FATAL_3":
                    case "RENAME_FILE_FATAL_3":
                        throw new FileNotFoundException("File or folder not found");
                    case "LOGIN_FATAL_2":
                        throw new ApplicationException("Access to the beta version is restricted for selected users only.");
                    case "LOGIN_FATAL_3":
                        throw new ApplicationException("The user account is disabled or banned.");
                    case "FILE_LINK_FATAL_3":
                        throw new IOException("Password required for download file"); // TODO: specific exception and catch it in WsAccountAccessor.UploadFile
                    case "FILE_LINK_FATAL_4":
                        throw new IOException("File temporarily unavailable.");
                    case "FILE_LINK_FATAL_5":
                        throw new IOException("Too many running downloads on too many devices.");
                    case "MOVE_FILE_FATAL_4":
                        throw new IOException("You do not have enough space on your private storage.");
                    case "RENAME_FILE_FATAL_6":
                        throw new IOException("Blacklisted file name and/or size.");
                    default:
                        throw new HttpRequestException($"Server return error status {result.Status} with code {result.ErrorCode}.");
                }
            }
            return true;
        }

        private int CalculateBuffer(long streamSize)
        {
            if (streamSize > 10 * 1024 * 1024)
                return 64 * 1024;
            if (streamSize > 32 * 1024)
                return 32 * 1024;
            return (int)streamSize;
        }
    }
}
