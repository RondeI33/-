using System.Collections.Generic;

public interface IFireSource
{
    List<ShotData> CreateShots(int sourceIndex, int totalSources);
}
