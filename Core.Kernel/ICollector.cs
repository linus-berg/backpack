// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Messages;
using MassTransit;

namespace Core.Kernel;

/// <summary>
///   Defines a collector module that consumes <see cref="ArtifactCollectRequest" /> messages.
/// </summary>
public interface ICollector : IConsumer<ArtifactCollectRequest> {
}