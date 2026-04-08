using Jobsite.Modules.HRWorkflows.Domain.Entities;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public interface IFeedbackAggregationService
{
    string? AggregateRecommendation(IReadOnlyList<InterviewPanelist> panelists);
}
