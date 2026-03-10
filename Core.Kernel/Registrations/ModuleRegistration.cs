// Copyright (c) 2022 Linus Berg. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using Core.Kernel.Constants;

namespace Core.Kernel.Registrations;

/// <summary>
/// Contains registration information for a system module, including its type, consumer, and endpoints.
/// </summary>
public class ModuleRegistration {
  /// <summary>
  /// The name of the module, derived from the entry assembly.
  /// </summary>
  public readonly string name;
  private readonly string prefix_;

  /// <summary>
  /// Initializes a new instance of the <see cref="ModuleRegistration"/> class.
  /// </summary>
  /// <param name="type">The type of the module.</param>
  /// <param name="consumer">The type of the message consumer for the module.</param>
  public ModuleRegistration(ModuleType type, Type consumer) {
    prefix_ = type.ToString().ToLower();
    this.consumer = consumer;
    name = Assembly.GetEntryAssembly().GetName().Name;
  }

  /// <summary>
  /// Gets the type of the message consumer.
  /// </summary>
  public Type consumer { get; }
  /// <summary>
  /// Gets the list of endpoints registered for this module.
  /// </summary>
  public List<Endpoint> endpoints { get; } = new();

  /// <summary>
  /// Adds a new endpoint to the module registration.
  /// </summary>
  /// <param name="name">The name of the endpoint (will be prefixed with the module type).</param>
  /// <param name="concurrency">The concurrency limit for the endpoint.</param>
  public void AddEndpoint(string name, int concurrency = 10) {
    endpoints.Add(
      new Endpoint {
        name = $"{prefix_}-{name}",
        concurrency = concurrency
      }
    );
  }
}
