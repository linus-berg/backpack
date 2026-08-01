// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Backpack.Tester;
using Backpack.Tester.Tests;

// ============================================================
// Backpack Tester
// ============================================================
// Uncomment the test you want to run. Only one should be active
// at a time. Tests that need S3/Minio require the BP_S3_*
// environment variables to be set.
// ============================================================

// --- WebMirror: mirrors a website to S3 ---
await WebMirrorTest.Run("https://ash-speed.hetzner.com/");

// --- Tests that use the shared service provider ---
// (IServiceProvider sp, HttpClient hc) = ServiceSetup.Build();

// --- HuggingFace: process model artifact + download files ---
// await HuggingFaceTest.Run(sp, hc);

// --- PyPI: process a Python package ---
// await PypiTest.Run(sp);

// --- Terraform: process a Terraform provider ---
// await TerraformTest.Run(sp);

// --- Git: mirror a git repository ---
// await GitTest.Run(sp);

// --- Skopeo: list container tags / copy to tar ---
// await SkopeoTest.Run(sp);

// --- Minio: debug S3 connection config ---
// await MinioTest.Run();

Console.WriteLine("No test selected. Uncomment a test in Program.cs to run it.");