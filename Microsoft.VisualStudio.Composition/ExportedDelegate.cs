namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Validation;

    public class ExportedDelegate
    {
        private readonly object target;
        private readonly MethodInfo method;

        public ExportedDelegate(object target, MethodInfo method)
        {
            Requires.NotNull(method, "method");

            this.target = target;
            this.method = method;
        }

        public Delegate CreateDelegate(Type delegateType)
        {
            Requires.NotNull(delegateType, "delegateType");

            if (delegateType == typeof(Delegate) || delegateType == typeof(MulticastDelegate))
            {
                delegateType = this.CreateStandardDelegateType();
            }
            try
            {
                return this.method.CreateDelegate(delegateType, this.target);
            }
            catch (ArgumentException)
            {
                // Bind failure occurs return null;
                return null;
            }
        }

        private Type CreateStandardDelegateType()
        {
            ParameterInfo[] parameters = this.method.GetParameters();

            // This array should contains a lit of all argument types, and the last one is the return type (could be void)
            Type[] parameterTypes = new Type[parameters.Length + 1];
            parameterTypes[parameters.Length] = this.method.ReturnType;
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            return Expression.GetDelegateType(parameterTypes);
        }
    }
}
