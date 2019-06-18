# PostItLater
## Features
* Schedule comments and self/link posts.
* Gracefully handles RATE_LIMIT. If a post/comment is prevented due to rate limiting, the post/comment will be rescheduled after the rate limit ends.
* Application is run on your own computer. Comments and links are posted under your own account. No information is ever sent outside of your network. 
## Requirements
* Tampermonkey for Chrome (untested on Greasemonkey)
* .NET >= 4.7.2
## Install
1. Download and run PostItLater 
2. Install [the userscript](https://greasyfork.org/en/scripts/386522-postitlater) using tampermonkey (greasemonkey and others not tested)
3. A datepicker and button labeled "Later" will now appear on old.reddit.com comment reply boxes and when submitting a link.
