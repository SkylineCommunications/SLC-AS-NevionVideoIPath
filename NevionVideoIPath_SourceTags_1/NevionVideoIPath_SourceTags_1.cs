using Skyline.DataMiner.Analytics.GenericInterface;

[GQIMetaData(Name = "Nevion VideoIPath Source Tags")]
public class MyDataSource : IGQIDataSource, IGQIOnInit
{
	private GQIDMS dms;

	public GQIColumn[] GetColumns()
	{
		return new GQIColumn[]
		{
			new GQIStringColumn("Tag"),
		};
	}

	public GQIPage GetNextPage(GetNextPageInputArgs args)
	{
		var rows = new GQIRow[] {
			new GQIRow(
				new GQICell[] {
					new GQICell() { Value = "Tag 1" },
				}),
			new GQIRow(
				new GQICell[] {
					new GQICell() { Value = "Tag 2" },
				}),
			new GQIRow(
				new GQICell[] {
					new GQICell() { Value = "Tag 3" },
				}),
		};

		return new GQIPage(rows)
		{
			HasNextPage = false,
		};
	}

	public OnInitOutputArgs OnInit(OnInitInputArgs args)
	{
		dms = args.DMS;
		return new OnInitOutputArgs();
	}
}