namespace ExtractionButtonMod.Configuration
{
    /// <summary>
    /// Defines which physical button(s) on an <see cref="ExtractionPoint"/> can be used
    /// to confirm and trigger extraction in standard gameplay levels.
    /// </summary>
    public enum ConfirmationButtonMode
    {
        /// <summary>
        /// Uses the main button built into the extraction point machine itself (default).
        /// </summary>
        ExtractionPointButton = 0,

        /// <summary>
        /// Keeps the Shop's confirmation button pedestal (<c>shopStation</c>) in standard levels
        /// and uses it to confirm extraction.
        /// </summary>
        ShopPedestalButton = 1,

        /// <summary>
        /// Enables both the built-in extraction machine button and the Shop pedestal button.
        /// </summary>
        Both = 2
    }
}

