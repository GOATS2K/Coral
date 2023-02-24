using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Services
{
    public interface IPaginationService
    {
        public Task<PaginatedData<TDtoType>> PaginateQuery<TSourceType, TDtoType>(int offset = 0, int limit = 10)
            where TSourceType : BaseTable
            where TDtoType : class;

        public Task<PaginatedData<TDtoType>> PaginateQuery<TSourceType, TDtoType>(Func<DbSet<TSourceType>, IQueryable<TSourceType>> sourceQuery, int offset = 0, int limit = 10)
            where TSourceType : BaseTable
            where TDtoType : class;
    }

    public class PaginationService : IPaginationService
    {
        private readonly IMapper _mapper;
        private readonly CoralDbContext _context;

        public PaginationService(IMapper mapper, CoralDbContext context)
        {
            _mapper = mapper;
            _context = context;
        }

        public async Task<PaginatedData<TDtoType>> PaginateQuery<TSourceType, TDtoType>(Func<DbSet<TSourceType>, IQueryable<TSourceType>> sourceQuery, int offset = 0, int limit = 10)
            where TSourceType : BaseTable
            where TDtoType : class
        {
            var dbSet = _context.Set<TSourceType>();
            var contextSet = sourceQuery(dbSet);
            var totalItemCount = await contextSet.CountAsync();
            var query = contextSet
                .OrderBy(i => i.Id)
                .Skip(offset)
                .Take(limit);

            var availableRecords = Math.Max(0, totalItemCount - (offset + limit));
            var querySize = await query.CountAsync();
            var data = await query
                .ProjectTo<TDtoType>(_mapper.ConfigurationProvider)
                .ToListAsync();

            return new PaginatedData<TDtoType>()
            {
                AvailableRecords = availableRecords,
                ResultCount = querySize,
                TotalRecords = totalItemCount,
                Data = data
            };
        }

        public async Task<PaginatedData<TDtoType>> PaginateQuery<TSourceType, TDtoType>(int offset = 0, int limit = 10)
            where TSourceType : BaseTable
            where TDtoType : class
        {
            var contextSet = _context.Set<TSourceType>();
            var totalItemCount = await contextSet.CountAsync();
            var query = contextSet
                .OrderBy(i => i.Id)
                .Skip(offset)
                .Take(limit);
            var availableRecords = Math.Max(0, totalItemCount - (offset + limit));
            var querySize = await query.CountAsync();
            var data = await query
                .ProjectTo<TDtoType>(_mapper.ConfigurationProvider)
                .ToListAsync();

            return new PaginatedData<TDtoType>()
            {
                AvailableRecords = availableRecords,
                ResultCount = querySize,
                TotalRecords = totalItemCount,
                Data = data
            };
        }
    }
}
