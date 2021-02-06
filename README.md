
# MaFi.WebShareCz.ApiClient

MaFi.WebShareCz.ApiClient is a .NET Standard 2.0 library with API Client for access files in WebShare.cz store.

[![Nuget](https://img.shields.io/nuget/vpre/MaFi.WebShareCz.ApiClient.svg?style=flat)](https://www.nuget.org/packages/MaFi.WebShareCz.ApiClient/)
![License](https://img.shields.io/github/license/ficnar/WebShareCz.ApiClient.svg)

## Nuget

```powershell
Install-Package MaFi.WebShareCz.ApiClient
```
## Using

Main entry point for using this component is create an instance of the class:

```C#
namespace MaFi.WebShareCz.ApiClient
{
	public class WsApiClient {}
}
```

Next, you must ensure user login by method:

```C#
public Task<bool> Login(string userName, ISecretProvider secretProvider, ISecretPersistor secretPersistor = null) {}
```

Then you can use property for access private and public root folder:

```C#
public WsFolder PrivateRootFolder { get; }
public WsFolder PublicRootFolder { get; }
```

## WebShare.cz

For store files must have an account on cloud WebShare.cz

More infos: [WebShare.cz Pages](https://webshare.cz)
