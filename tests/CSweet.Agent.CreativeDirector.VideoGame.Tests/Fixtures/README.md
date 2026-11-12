# SHATTERCHORD GDD incident fixture

`shatterchord-gdd-2026-11-12.excerpt.md` retains the seven top-level sections and
their first paragraphs/subheadings verbatim from the failed 33,902-character model
reply on 2026-11-12. Omitted section bodies do not affect its heading-level failure.
This is an excerpt, not the complete reply or a newly generated GDD.
Current regression tests submit this text unchanged for independent semantic review;
the heading mismatch is no longer a validation failure or a reason to regenerate it.

Source: Super Awesome Games, Naomi 4.1.2, `creative-design` model response event
`a76d497c-1f4d-48ff-9bf7-2e9f17d7cb27` at 17:42:56 UTC. The prompt named the seven
sections without specifying heading levels. The model returned `#` sections with
`##` subsections; validation required the seven section names at level two.

The Director regression uses the subsequent exact blocked reply and earlier
submission/feedback sequence. Finalization failed three times with
`A finalization turn cannot continue the collaboration.`; the last diagnostic was
`a8cc978a-c794-4121-9528-50ded1a56b1d`.
