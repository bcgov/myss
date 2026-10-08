namespace Myss.Api.Data
{
    using System;
    using Myss.Api.Models;

    /// <summary>
    /// A phone number on a citizen's account, entered on Account Info (MYSS-271).
    /// </summary>
    public class MyssUserPhone
    {
        /// <summary>Gets or sets the row identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the owning profile.</summary>
        public Guid ProfileId { get; set; }

        /// <summary>Gets or sets which kind of number this is. One number per type.</summary>
        public AccountPhoneType Type { get; set; }

        /// <summary>Gets or sets the ten digits, formatting stripped.</summary>
        public required string Number { get; set; }

        /// <summary>Gets or sets where the number sits in the citizen's list, from 0.</summary>
        public int Position { get; set; }
    }
}
