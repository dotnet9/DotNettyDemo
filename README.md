# DotNettyDemo

- DotNetty测试程序，包含一个服务端和客户端
- 皆采用 .NET 5 WPF 开发
- 数据压缩使用 Google ProtocolBuffers

工具路径：F:\code\DotNettyDemo\src\Google Protocbuffer

ChatInfo.proto
```
syntax = "proto3";
 
package DotNetty.Models;
 
message ChatInfo {
  int32 Code = 1;
  int64 Time = 2;
  string Msg = 3;
  string FromId = 4;
  string ReqId = 5;
  string Data = 6;
}
```

使用protoc.exe生成ChatInfo.cs文件，通信对象生成命令如下：
```
protoc.exe --csharp_out . ChatInfo.proto
```