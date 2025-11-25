using System.ComponentModel.DataAnnotations;

namespace FtoConsulting.PortfolioManager.Api.Models.Requests;

/// <summary>
/// API request model for updating holding units
/// </summary>
public class UpdateHoldingUnitsApiRequest
{
    /// <summary>
    /// New number of units for the holding
    /// </summary>
    [Required]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Units must be greater than 0")]
    public decimal Units { get; set; }
}