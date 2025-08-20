using System;
using System.Collections.Generic;

namespace Api_Celero.Models
{
    public class DashboardMetrics
    {
        public DateTime GeneratedAt { get; set; }
        public string Period { get; set; } = string.Empty;
        public GeneralMetrics General { get; set; } = new();
        public SecurityMetrics Security { get; set; } = new();
        public List<HourlyActivity> HourlyActivity { get; set; } = new();
        public List<TopMetric> TopFailedIPs { get; set; } = new();
        public List<TopMetric> TopSharedFingerprints { get; set; } = new();
        public List<RecentActivity> RecentSuspiciousActivity { get; set; } = new();
        public PaymentMetrics PaymentStats { get; set; } = new();
    }

    public class GeneralMetrics
    {
        public int TotalEvents { get; set; }
        public int UniqueClients { get; set; }
        public int UniqueSessions { get; set; }
        public int TotalRequests { get; set; }
        public double AverageResponseTime { get; set; }
    }

    public class SecurityMetrics
    {
        public int SuspiciousIPs { get; set; }
        public int SharedFingerprints { get; set; }
        public int MultipleTimeZoneClients { get; set; }
        public int RapidFireAttempts { get; set; }
        public int FailedPayments { get; set; }
        public double ThreatScore { get; set; } // 0-100
    }

    public class PaymentMetrics
    {
        public int TotalAttempts { get; set; }
        public int SuccessfulPayments { get; set; }
        public int FailedPayments { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal SuccessAmount { get; set; }
        public double SuccessRate { get; set; }
        public Dictionary<string, int> PaymentMethodBreakdown { get; set; } = new();
        public Dictionary<string, int> ErrorCodeBreakdown { get; set; } = new();
    }

    public class HourlyActivity
    {
        public DateTime Hour { get; set; }
        public int Events { get; set; }
        public int FailedPayments { get; set; }
        public int SuccessfulPayments { get; set; }
        public int UniqueClients { get; set; }
    }

    public class TopMetric
    {
        public string Identifier { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime LastSeen { get; set; }
        public List<string> RelatedClients { get; set; } = new();
        public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
    }

    public class RecentActivity
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info"; // Info, Warning, Critical
    }

    public class RealTimeMetric
    {
        public string MetricType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double PreviousValue { get; set; }
        public double ChangePercent { get; set; }
        public string Trend { get; set; } = "stable"; // up, down, stable
        public DateTime Timestamp { get; set; }
    }

    public class LiveDashboard
    {
        public List<RealTimeMetric> Metrics { get; set; } = new();
        public List<RecentActivity> RecentEvents { get; set; } = new();
        public int ActiveSessions { get; set; }
        public int EventsPerMinute { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}