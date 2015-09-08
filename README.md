# GitItemRepositoryProofOfConcept
Proof of Concept for use of Git / GitLab as an item repository (item pool) for Smarter Balanced item development and assessment production. This application draws items from a [test content package in SmarterApp format](http://www.smarterapp.org/specs/SmarterApp_ItemPackaging.html) and pushes them into GitLab under a designated group name.

## Implementation Notes
The test installation of GitLab that was set up for this test has a limited amount of storage. To provide a test with a large number of items (10,000 or more) without overflowing available storage, this test omits large multimedia files such as ASL videos and audio files.

Out of expediency, paths to certain applications such as the local Git installation are hard-coded. Others using the software will have to correct this deficiency. Nevertheless, the source code is posted because it is a valid basis for other tools to follow.

## Build Notes
Written in C#. Built using the free Microsoft Visual Studio Express 2013 for Windows Desktop. Other editions of Visual Studio should also work.
