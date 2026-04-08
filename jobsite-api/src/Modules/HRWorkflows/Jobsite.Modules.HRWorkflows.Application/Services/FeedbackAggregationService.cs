using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public sealed class FeedbackAggregationService : IFeedbackAggregationService
{
    public string? AggregateRecommendation(IReadOnlyList<InterviewPanelist> panelists)
    {
        List<InterviewPanelist> submittedPanelists = panelists
            .Where(p => p.FeedbackSubmittedAt is not null && p.Recommendation is not null)
            .ToList();

        if (submittedPanelists.Count == 0)
            return null;

        Dictionary<string, int> votes = new()
        {
            [InterviewRecommendation.StrongHire] = 0,
            [InterviewRecommendation.Hire] = 0,
            [InterviewRecommendation.NoHire] = 0,
            [InterviewRecommendation.StrongNoHire] = 0
        };

        foreach (InterviewPanelist panelist in submittedPanelists)
        {
            if (votes.ContainsKey(panelist.Recommendation!))
                votes[panelist.Recommendation!]++;
        }

        string topRecommendation = votes
            .OrderByDescending(v => v.Value)
            .First()
            .Key;

        return topRecommendation;
    }
}
