// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace User
{
    using System;
    using System.Composition;
    using Garage;

    [Export]
    public class Importer
    {
        [Import]
        [ImportMetadataConstraint("Year", 2016)]
        private Lazy<Car>? NewerCar { get; set; }

        [Import]
        [ImportMetadataConstraint("Type", "Used")]
        private Car? UsedCar { get; set; }
    }
}
