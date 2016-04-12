﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace DatabaseBackup.Entities
{
    public class UniqueConstraint : Constraint
    {
        public override string GetCreationQuery()
        {
            return $"ALTER TABLE [{this.TableSchema}].[{this.TableName}] ADD CONSTRAINT {this.Name} UNIQUE ({string.Join(", ", this.Columns)})";
        }
    }
}