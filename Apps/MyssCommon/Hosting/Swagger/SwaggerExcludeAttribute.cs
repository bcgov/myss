namespace Myss.Common.Hosting.Swagger
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Excludes the specified controller.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SwaggerExcludeAttribute : Attribute;
}
