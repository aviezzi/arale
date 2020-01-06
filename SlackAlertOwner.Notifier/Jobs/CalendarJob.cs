﻿namespace SlackAlertOwner.Notifier.Jobs
{
    using Abstract;
    using Model;
    using NodaTime;
    using Quartz;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    [DisallowConcurrentExecution]
    public class CalendarJob : IJob
    {
        readonly IAlertOwnerService _alertOwnerService;
        readonly ITypeConverter<LocalDate> _converter;
        readonly ISlackHttpClient _httpClient;
        readonly ILogService _logger;
        readonly IShiftsService _shiftsService;

        public CalendarJob(IAlertOwnerService alertOwnerService, ISlackHttpClient httpClient,
            IShiftsService shiftsService, ITypeConverter<LocalDate> converter,
            ILogService logger)
        {
            _alertOwnerService = alertOwnerService;
            _httpClient = httpClient;
            _shiftsService = shiftsService;
            _converter = converter;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.Log("Start CalendarJob");

            var teamMates = (await _alertOwnerService.GetTeamMates()).ToList();
            var oldCalendar = (await _alertOwnerService.GetCalendar(teamMates)).ToList();

            var patronDays = await _alertOwnerService.GetPatronDays();
            var shiftService = _shiftsService
                .AddPatronDays(patronDays);

            IEnumerable<Shift> calendar;

            if (oldCalendar.Any())
                calendar = shiftService
                    .Build(oldCalendar)
                    .ToList();
            else
                calendar = shiftService
                    .Build(teamMates)
                    .ToList();

            await _alertOwnerService.ClearCalendar();
            await _alertOwnerService.WriteCalendar(calendar);

            await _httpClient.Notify("Ciao <!channel> e' uscito il nuovo calendario dei turni:");
            await _httpClient.Notify(calendar.Select(shift =>
                $"{_converter.FormatValueAsString(shift.Schedule)} - {shift.TeamMate.Name}"));

            _logger.Log("CalendarJob Completed");
        }
    }
}