namespace SwiftMessageProcessor.Core.Models;

/// <summary>
/// Enumeration for SWIFT charge bearer codes (Field 71A)
/// </summary>
public enum ChargeBearer
{
    /// <summary>
    /// BEN - Beneficiary bears all charges
    /// </summary>
    BEN,
    
    /// <summary>
    /// OUR - Ordering customer bears all charges
    /// </summary>
    OUR,
    
    /// <summary>
    /// SHA - Charges are shared between ordering customer and beneficiary
    /// </summary>
    SHA
}