using FtoConsulting.PortfolioManager.Application.DTOs;

namespace FtoConsulting.PortfolioManager.Application.Services;

/// <summary>
/// Thrown when a portfolio report generation is requested while one of the same type is already in-flight.
/// </summary>
public sealed class ReportAlreadyInProgressException(ReportType reportType)
    : InvalidOperationException($"A {reportType} report is already being generated.");
