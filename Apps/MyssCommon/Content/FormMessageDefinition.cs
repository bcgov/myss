namespace Myss.Common.Content
{
    using System.Collections.Generic;

    /// <summary>
    /// A form message that MySS Administration can edit and a form's page reads: where
    /// it is stored, how editors see it, and the placeholders it may use.
    /// </summary>
    /// <param name="FormSpecId">The form the message belongs to.</param>
    /// <param name="MessageKey">The message's name within the form, such as <c>confirmation</c>.</param>
    /// <param name="Name">What editors call the message.</param>
    /// <param name="Description">Where applicants see the message.</param>
    /// <param name="Placeholders">The placeholders the message may use.</param>
    public sealed record FormMessageDefinition(
        string FormSpecId,
        string MessageKey,
        string Name,
        string Description,
        IReadOnlyList<FormMessagePlaceholder> Placeholders);

    /// <summary>A <c>{name}</c> placeholder that the page replaces with a value.</summary>
    /// <param name="Name">The name between the braces.</param>
    /// <param name="Description">What the value is, as it reads within a sentence.</param>
    /// <param name="Sample">The value the editor's preview shows.</param>
    /// <param name="Required">Whether every saved message must contain it.</param>
    public sealed record FormMessagePlaceholder(string Name, string Description, string Sample, bool Required);
}
