// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:property TargetFramework=net11.0

using System.Text.Json.Nodes;

using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(1));

var token = cancellation.Token;

var version = Environment.GetEnvironmentVariable("RELEASE_VERSION") ?? throw new InvalidOperationException("RELEASE_VERSION is required.");

var path = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? Directory.GetCurrentDirectory(), "version.json");

var json = JsonNode.Parse(await File.ReadAllTextAsync(path, token)) ?? throw new InvalidOperationException("version.json is empty.");

json["version"] = version;

await File.WriteAllTextAsync(path, json.ToJsonString(), token);
