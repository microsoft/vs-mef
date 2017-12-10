// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Resources;

#if DESKTOP
[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
#else
[assembly: NeutralResourcesLanguage("en-US")]
#endif

[assembly: CLSCompliant(true)]
