namespace LibraryBox.Web;

using Azure.Data.Tables;

public static class TableClientExtensions
{
	public static async Task<T?> GetEntityOrDefaultAsync<T>(this TableClient table, string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default) where T : class, ITableEntity
	{
		partitionKey = partitionKey ?? throw new ArgumentNullException("partitionKey");
		rowKey = rowKey ?? throw new ArgumentNullException("rowKey");
		string filter = TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("PartitionKey", "eq", partitionKey), "and", TableQuery.GenerateFilterCondition("RowKey", "eq", rowKey));
		var page = await table.QueryAsync<T>(filter, 1, select, cancellationToken)
			.AsPages(null, 1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(continueOnCapturedContext: false);
		return (page != null && page.Values.Count > 0) ? page.Values[0] : null;
	}

}
