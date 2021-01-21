// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public struct DiscoveryProgress
    {
        public DiscoveryProgress(int completedSteps, int totalSteps, string status)
            : this()
        {
            this.CompletedSteps = completedSteps;
            this.TotalSteps = totalSteps;
            this.Status = status;
        }

        public int CompletedSteps { get; private set; }

        public int TotalSteps { get; private set; }

        public float Completion
        {
            get { return this.TotalSteps > 0 ? ((float)this.CompletedSteps / this.TotalSteps) : 0; }
        }

        public string Status { get; private set; }
    }
}
