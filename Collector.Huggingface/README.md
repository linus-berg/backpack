1. Only store the latest version of each model and files.
2. Processor produces hf:// prefixed files.
3. The processor should always produce a NEW version and DELETE the old one if they differ, each version is equal to a revision.
4. Collector does HEAD calls to the huggingface API to get ETAG about each file in a specific REVISION.
5. The collector should always overwrite the old file if the ETAG differ.

Verify: Gateway deep comparison must work when versions get DELETED and ADDED.
TODO: Implement the collection logic for the HuggingFace API.