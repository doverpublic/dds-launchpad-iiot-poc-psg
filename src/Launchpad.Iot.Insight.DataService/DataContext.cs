using System;
using System.Collections.Generic;
using System.Data.Entity;

using System.Linq;
using System.Threading.Tasks;


using Launchpad.Iot.Insight.DataService.Models;

namespace Launchpad.Iot.Insight.DataService
{
    public class DataContext : DbContext
    {
        public DataContext() : base( "name=InMemoryDB"){}

        public DbSet<User> Users { get; set; }
    }
}
