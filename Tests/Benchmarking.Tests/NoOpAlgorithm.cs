// Copyright (c) Ulf Bourelius. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using BenchmarkingSandbox.Core;

namespace BenchmarkingSandbox.Tests
{
    internal class NoOpAlgorithm : IAlgorithm
    {
        public string Name => "NoOpAlgorithm";

        public void Execute(object input)
        {
            // No operation performed
            // This is a placeholder for an algorithm that does nothing
            // In a real scenario, you might want to log or perform some other action
        }
    }
}
