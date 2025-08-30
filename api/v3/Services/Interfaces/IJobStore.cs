using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchoolETL.Worker;

namespace SchoolETL.Services.Interfaces
{
    public interface IJobStore
    {

        ImportJob? Get(Guid id);
        void Upsert(ImportJob job);
    }
}