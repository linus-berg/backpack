// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Core.Kernel.Messages;
using MassTransit;

namespace Core.Kernel;

/// <summary>
///   Defines a processor module that consumes <see cref="ArtifactProcessRequest" /> messages and
///   <see cref="ArtifactPreviewRequest" /> messages.
/// </summary>
public interface IProcessor : IConsumer<ArtifactProcessRequest>,
                              IConsumer<ArtifactPreviewRequest> {
}