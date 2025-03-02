﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

[ApiController]
[Route("api/DataApi")]
public class DataApiController : ControllerBase
{
    private readonly TcmbService _tcmbService;
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _redis;

    public DataApiController(TcmbService tcmbService, ApplicationDbContext context, IConnectionMultiplexer redis)
    {
        _tcmbService = tcmbService;
        _context = context;
        _redis = redis;
    }

    [HttpGet("fetch")]
    public async Task<IActionResult> FetchExchangeRates()
    {
        try
        {
            await _tcmbService.FetchAndStoreExchangeRatesAsync(_context);

            return Ok(new
            {
                Message = "Exchange rates fetched and saved successfully.",
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while fetching exchange rates.", Error = ex.Message });
        }
    }

    [HttpGet("{currencyCode}")]
    [Produces("application/json", "application/xml")]
    public async Task<IActionResult> GetExchangeRates(string currencyCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                return BadRequest(new { Message = "Currency code cannot be empty." });
            }

            currencyCode = currencyCode.ToUpper().Trim();
            var today = DateTime.Today;
            var startDate = today.AddMonths(-2);
            var exchangeRates = new List<ExchangeRate>();

            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                if (_tcmbService.IsWeekend(date) || _tcmbService.IsHoliday(date))
                {
                    continue;
                }

                var rates = await _tcmbService.GetExchangeRatesForCurrencyAsync(date, currencyCode);
                if (rates != null)
                {
                    exchangeRates.AddRange(rates.Where(r => r.CurrencyCode == currencyCode));
                }
            }

            if (!exchangeRates.Any())
            {
                return NotFound(new { Message = $"No exchange rates found for currency: {currencyCode} in the last two months." });
            }

            return Ok(exchangeRates);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching exchange rates: {ex.Message}");
            return StatusCode(500, new { Message = "An error occurred while retrieving exchange rates.", Error = ex.Message });
        }
    }
}