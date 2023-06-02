# 🔐 AspNetCryptoMiddleware 

Examples of aspnet core middlewares that **encode/encrypt/decode/decrypt** a request/response body based on _ContentType_


There is some implementations in diferents branchs:

- `simple-base64` -> very basic base64 encoding/decoding using **[Convert](https://learn.microsoft.com/en-us/dotnet/api/system.convert.frombase64string)** and **[Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream)**
- `better-base64` -> base64 encoding/decoding using **[CryptoStream](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptostream)**
- `adv-base64` -> base64 encoding/decoding using [Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines)
- `simple-aes128` -> [Aes123](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes) encryption/decryption using **[CryptoStream](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptostream)**
- `adv-aes128` (*this*) -> [Aes123](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes) encryption/decryption using _CryptoStream_ and **[Pipelines](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines)**
