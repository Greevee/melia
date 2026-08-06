-- Store the rank a job's current circle was taken at, for the class circle
-- system. Ranks used to be derived from the order jobs were selected, which
-- renumbered the jobs taken before a circle advancement and moved them onto a
-- different EXP curve. 0 means "not assigned yet" and is backfilled from the
-- derived order the first time the character is loaded.
-- Named `jobRank` rather than `rank`, which is reserved as of MySQL 8.0.2.
ALTER TABLE `jobs` ADD COLUMN `jobRank` int(11) NOT NULL DEFAULT '0' AFTER `circle`;

-- Same for the rollback snapshots, so restoring a character doesn't drop the
-- ranks and send its jobs back onto derived ones.
ALTER TABLE `snapshot_jobs` ADD COLUMN `jobRank` int(11) NOT NULL DEFAULT '0' AFTER `circle`;
