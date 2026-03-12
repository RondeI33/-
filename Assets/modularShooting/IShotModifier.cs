using System.Collections.Generic;

public interface IShotModifier
{
    List<ShotData> ProcessShots(List<ShotData> shots);
}
