# Video editing source

`full_draft_v9` contains the latest Premiere project, XML interchange timeline,
caption images/SRT, stills, visual assets and editing manifests. The JSON manifests
use paths relative to this folder. No generated Unity cache is required to retain
these editing sources.

Open `full_draft_v9/CardArchive_Full_Draft_v9.prproj` in Premiere. Its original
media references point to `Library/VideoAnalysis/full_draft_v9`; use Locate Media
to relink the matching folders here if Premiere reports offline media. The XML
timeline retains its original path URLs and can also be relinked after import.

Source footage (`sources/*.mp4`), rendered segments (`media`, `render`) and exported
movies remain local and are ignored by Git. A fresh clone needs those media files
from the author before exporting the complete video. The native project and
interchange timeline have been retained unchanged.

AI editing notes and subtitle drafts are maintained in [AIWork](../../../AIWork/README.md).
The obsolete Unity `DemoVideoExport` helper was removed: it only targeted the old
`full_draft_v5` export and is not needed to edit the retained v9 project.
