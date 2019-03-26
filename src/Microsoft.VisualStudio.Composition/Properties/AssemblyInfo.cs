// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Resources;

#if !(NETSTANDARD1_5 || NETCOREAPP1_0)
[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
#else
[assembly: NeutralResourcesLanguage("en-US")]
#endif

[assembly: CLSCompliant(true)]
