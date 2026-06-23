namespace Myss.Api.Models
{
    using System;

    /// <summary>
    /// Represents the result of a request.
    /// </summary>
    public class BaseResponseModel<T>
    {
        /// <summary>
        /// Gets or sets the payload information.
        /// </summary>
        public required T Payload { get; set; }

        /// <summary>
        /// Gets or sets the payload information.
        /// </summary>
        public required DateTime DatetimeRequested { get; set; }
    }
}
