// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Garage
{
    using System;

    public class Car
    {
        protected string? Name { get; set; }

        protected int? Year { get; set; }

        public void PrintInfo()
        {
            Console.WriteLine("Name: " + this.Name + ", Year: " + this.Year);
        }
    }
}
