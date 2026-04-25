// See https://aka.ms/new-console-template for more information

using Collector.Git;
using Collector.Http;
using Collector.Kernel;
using Collector.Kernel.Storage.Minio;
using Core.Kernel;
using Core.Kernel.Models;
using Library.Github;
using Library.Skopeo;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Timeout;
using Processor.HuggingFace;
using Processor.Pypi;
using Processor.Terraform;

HttpClient hc = new(
  new HttpClientHandler {
    /*Proxy = new WebProxy() {
      Address = new Uri(Environment.GetEnvironmentVariable("HTTP_PROXY"))
    },*/
    AllowAutoRedirect = true
  }
);
hc.DefaultRequestHeaders.Add("User-Agent", "Backpack/1.0");
/*HttpResponseMessage res = await hc.GetAsync(
                            "https://api.github.com/repos/Shardj/zf1-future/zipball/b87c1507cd10c01d9b3b1bc4a0cae32f6a9c6d6c");

HttpResponseMessage res2 =
  await hc.GetAsync(
    "https://registry.npmjs.org/@geoext/geoext/-/geoext-3.1.1.tgz");
*/
ServiceCollection services = new();
services.AddStorage();
// Define a resilience pipeline with the name "my-pipeline"
services.AddResiliencePipeline<string, bool>(
  "git-timeout",
  builder => {
    builder.AddTimeout(
      new
        TimeoutStrategyOptions {
          Timeout =
            TimeSpan.FromMinutes(10)
        }
    );
  }
);

services.AddLogging();
services.AddSingleton<FileSystem>();
services.AddSingleton<Git>();
services.AddSingleton<IPypi, Pypi>();
services.AddSingleton<ITerraform, Terraform>();
services.AddSingleton<IHuggingFace, HuggingFace>();
services.AddSingleton<IGithubClient, GithubClient>();
services.AddSingleton<SkopeoClient>();
// Build the service provider
IServiceProvider sp = services.BuildServiceProvider();

// Execute the pipeline
//Git git = sp.GetRequiredService<Git>();

IGithubClient gh = sp.GetRequiredService<IGithubClient>();
ITerraform tf = sp.GetRequiredService<ITerraform>();
IHuggingFace hf = sp.GetRequiredService<IHuggingFace>();

IPypi py = sp.GetRequiredService<IPypi>();

FileSystem fs = sp.GetRequiredService<FileSystem>();
SkopeoClient sk = sp.GetRequiredService<SkopeoClient>();

SkopeoListTagsOutput? tags = await sk.GetTags("docker.io/amazon/aws-cli");

if (tags != null) {
  foreach (string tag in tags.tags) {
    Console.WriteLine(tag);
  }
}
await sk.CopyToTar("docker-archive://docker.io/amazon/aws-cli:2.31.12", "docker-archive");

Artifact php_artifact = new() {
  id = "shardj/zf1-future",
  processor = "php",
  filter = string.Empty
};
Artifact py_artifact = new() {
  id = "pandas",
  processor = "pypi",
  filter = string.Empty
};

Artifact response = await py.ProcessArtifact(py_artifact);

//var art = await hub.ProcessArtifact(artifact);

php_artifact.config["files"] =
  @"^helm-v\d+.\d+.\d+-darwin-arm64.tar.gz.sha256sum.asc$";
//await ghr.ProcessArtifact(artifact);
//await git.Mirror("git://github.com/linus-berg/ATM.Npm");
string ind = "/storage/artifacts/mirrors/git/input";
string path =
  $"{ind}/github.com/linus-berg/test@101001-201023.bundle";

string url = "https://proxy.golang.org/golang.org/x/exp/@v/list";
RemoteFile file = new(url, fs);
//await fs.PutString("debug", "");
//await file.Get("debug");
// DEBUG MINIO
MinioConnectionBuilder connection = new();

connection.region =
  Configuration.GetBackpackVariable(CoreVariables.BP_S3_REGION);
connection.access_key =
  Configuration.GetBackpackVariable(CoreVariables.BP_S3_ACCESS_KEY);
connection.secret_key =
  Configuration.GetBackpackVariable(CoreVariables.BP_S3_SECRET_KEY);
connection.end_point =
  Configuration.GetBackpackVariable(CoreVariables.BP_S3_ENDPOINT);
connection.bucket =
  Configuration.GetBackpackVariable(CoreVariables.BP_S3_BUCKET);

//await st.SaveFileAsync("debug/empty-file", remote_stream);
//await file.Get("list");
Console.WriteLine("test");
Console.WriteLine(Path.GetDirectoryName(path));

Console.WriteLine(Path.GetDirectoryName(Path.GetRelativePath(ind, path)));
//await client.CopyToRegistry("docker://docker.io/registry:2");
Console.WriteLine("---");