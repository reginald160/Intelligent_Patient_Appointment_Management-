﻿using HMSPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;


namespace HMSPortal.Domain.Models.Contract
{
    public class AuditEntry
    {
        public EntityEntry Entry { get; }

        public string? UserId { get; set; }

        public string TableName { get; set; }

        public Dictionary<string, object> KeyValues { get; } = new Dictionary<string, object>();


        public Dictionary<string, object> OldValues { get; } = new Dictionary<string, object>();


        public Dictionary<string, object> NewValues { get; } = new Dictionary<string, object>();


        public List<PropertyEntry> TemporaryProperties { get; } = new List<PropertyEntry>();


        public AuditType AuditType { get; set; }

        public List<string> ChangedColumns { get; } = new List<string>();


        public bool HasTemporaryProperties => TemporaryProperties.Any();

        public AuditEntry(EntityEntry entry)
        {
            Entry = entry;
        }

        public BaseEntity ToAudit()
        {
            return new BaseEntity
            {
                UserId = UserId,
                Type = AuditType.ToString(),
                TableName = TableName,
                DateTime = DateTime.UtcNow,
                PrimaryKey = JsonConvert.SerializeObject(KeyValues),
                OldValues = OldValues.Count == 0 ? null : JsonConvert.SerializeObject(OldValues),
                NewValues = NewValues.Count == 0 ? null : JsonConvert.SerializeObject(NewValues),
                AffectedColumns = ChangedColumns.Count == 0 ? null : JsonConvert.SerializeObject(ChangedColumns)
            };
        }
    }
}
