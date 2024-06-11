// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition.Reflection;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using MessagePack;
using Microsoft.VisualStudio.Composition.Formatter;

[DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
[MessagePackObject]
public class PropertyRef : MemberRef, IEquatable<PropertyRef>
{
    /// <summary>
    /// Gets the string to display in the debugger watch window for this value.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => $"{this.DeclaringType.FullName}.{this.Name}";

    /// <summary>
    /// The metadata token for this member if read from a persisted assembly.
    /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
    /// </summary>
    private readonly int? getMethodMetadataToken;

    /// <summary>
    /// The metadata token for this member if read from a persisted assembly.
    /// We do not store metadata tokens for members in dynamic assemblies because they can change till the Type is closed.
    /// </summary>
    private readonly int? setMethodMetadataToken;

    public PropertyRef(TypeRef declaringType, TypeRef propertyTypeRef, int metadataToken, int? getMethodMetadataToken, int? setMethodMetadataToken, string name, bool isStatic)
        : base(declaringType, metadataToken, isStatic)
    {
        this.getMethodMetadataToken = getMethodMetadataToken;
        this.setMethodMetadataToken = setMethodMetadataToken;
        this.Name = name;
        this.PropertyTypeRef = propertyTypeRef;
    }

    [SerializationConstructor]
#pragma warning disable RS0016 // Add public types and members to the declared API, This was added to make the class serializable and avoid the breaking change
    public PropertyRef(TypeRef declaringType, TypeRef propertyTypeRef, int metadataToken, string name, bool isStatic, int? getMethodMetadataToken, int? setMethodMetadataToken)
#pragma warning restore RS0016 // Add public types and members to the declared API
        : this(declaringType, propertyTypeRef, metadataToken, getMethodMetadataToken, setMethodMetadataToken, name, isStatic)
    {
    }

    public PropertyRef(PropertyInfo propertyInfo, Resolver resolver)
               : base(propertyInfo, resolver)
    {
        this.getMethodMetadataToken = propertyInfo.GetMethod?.MetadataToken;
        this.setMethodMetadataToken = propertyInfo.SetMethod?.MetadataToken;
        this.Name = propertyInfo.Name;
        this.PropertyTypeRef = TypeRef.Get(propertyInfo.PropertyType, resolver);
    }

    [IgnoreMember]
    public PropertyInfo PropertyInfo => (PropertyInfo)this.MemberInfo;

    [Key(1)]
    public TypeRef PropertyTypeRef { get; }

    [Key(5)]
    public int? GetMethodMetadataToken => this.getMethodMetadataToken;

    [Key(6)]
    public int? SetMethodMetadataToken => this.setMethodMetadataToken;

    [Key(3)]
    public override string Name { get; }

    internal override void GetInputAssemblies(ISet<AssemblyName> assemblies) => this.DeclaringType?.GetInputAssemblies(assemblies);

    protected override bool EqualsByTypeLocalMetadata(MemberRef other)
    {
        var otherProperty = (PropertyRef)other;
        return this.Name == otherProperty.Name;
    }

    protected override MemberInfo Resolve() => ResolverExtensions.Resolve(this) ?? throw new InvalidOperationException($"Unable to find property {this.Name} on {this.DeclaringType.FullName}");

    public override int GetHashCode() => this.DeclaringType.GetHashCode() + this.Name.GetHashCode();

    public bool Equals(PropertyRef? other) => this.Equals((MemberRef?)other);
}
