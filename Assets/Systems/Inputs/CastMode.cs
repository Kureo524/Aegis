namespace Systems.Inputs {
    /// <summary>How R / F cast (design doc §7.4, player option).</summary>
    public enum CastMode {
        /// <summary>Press: aim (range + shape shown); left click: cast.</summary>
        Normal,
        /// <summary>Hold: aim; release: cast towards the cursor. Left click also casts, Escape / right click cancel.</summary>
        QuickWithIndicator,
        /// <summary>Press: cast at once towards the cursor, nothing shown.</summary>
        Quick,
    }
}
