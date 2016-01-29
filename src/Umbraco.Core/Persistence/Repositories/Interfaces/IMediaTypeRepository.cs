﻿using System.Collections.Generic;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Querying;

namespace Umbraco.Core.Persistence.Repositories
{
    public interface IMediaTypeRepository : IContentTypeCompositionRepository<IMediaType>
    {
        /// <summary>
        /// Gets all entities of the specified <see cref="PropertyType"/> query
        /// </summary>
        /// <param name="query"></param>
        /// <returns>An enumerable list of <see cref="IMediaType"/> objects</returns>
        IEnumerable<IMediaType> GetByQuery(IQuery<PropertyType> query);

        IEnumerable<MoveEventInfo<IMediaType>> Move(IMediaType toMove, EntityContainer container);
    }
}