# FitbitWebOSC
An extension for [HRtoVRChat_OSC](https://github.com/200Tigersbloxed/HRtoVRChat_OSC) to implement the Fitbit Web API.

## Installation
Run HRtoVRChat_OSC to generate the required folders, then place `FitbitWebOSC.dll` into the `HRtoVRChat_OSC\SDKs\` folder in the folder you installed the program to (Generally something like `HRtoVRChat_win-x64\HRtoVRChat_OSC\SDKs\`).

If you're using the HR to VRChat Launcher, then the SDK folder will be at `%AppData%\HRtoVRChatLauncher\HRtoVRChat\HRtoVRChat_OSC\SDKs`.

## Setup
### 1. Configuring HRtoVRChat_OSC
Set the value of `hrType` to `sdk` for using the extension. To see other options, check <https://github.com/200Tigersbloxed/HRtoVRChat_OSC#config>.

### 2. Creating a Fitbit developer application
Go to the page <https://dev.fitbit.com/apps/new> and start creating a new application, use the values listed below, accept the agreement, and make sure the `OAuth 2.0 Application Type` is still set to the correct value and click the `Register` button once you are done.

For the values of the fields, follow this table:
Name | Value | Explanation
--- | --- | ---
Application Name | HRtoVRChat_OSC | What you set for this doesn't matter as long as it doesn't contain `Fitbit`.
Description | An app to send Fitbit data to VRChat over OSC | What you set for this doesn't matter.
Application Website URL | https://github.com/ButterscotchV/FitbitWebOSC | A link to the application.
Organization | Butterscotch! | Whatever you want, a name of some sort.
Organization Website URL | https://github.com/ButterscotchV/FitbitWebOSC | A link to the application.
Terms of Service URL | https://github.com/ButterscotchV/FitbitWebOSC | A link to the application.
Privacy Policy URL | https://github.com/ButterscotchV/FitbitWebOSC | A link to the application.
OAuth 2.0 Application Type | Personal | This is very important, make sure this is set correctly as it's reset if you enter an invalid value anywhere.
Redirect URL | http://localhost:8080/ | This is the URL that the authentication will be sent to, this will redirect it to the extension.
Default Access Type | Read Only | Write access is not needed.

### 3. Configuring the extension
Refer to [the config documentation](#Config) for where to find the config and what options are available. Copy the `OAuth 2.0 Client ID` from the application details page and put it into the `ClientId` config (without the `<` or `>`), then copy the `Client Secret` and put it into the `ClientSecret` config (without the `<` or `>`).

### 4. Authorize the extension
Run the program and allow access to your heartrate data through the Fitbit page that is opened in your browser, this should finish the setup of the extension and allow usage of your Fitbit data on your local computer.

## Config
The config file is auto-generated at `%AppData%\FitbitWebOSC\fitbit_web_config.json` when you run the extension, formatting will not be preserved.

Config Option | Value Type | Default Value | Description
--- | :---: | :---: | ---
last_run_version | String | **Dynamic** | The last version of the extension that was run (for information purposes only).
fitbit_credentials.ClientId | String | "\<OAuth 2.0 Client ID\>" | The Fitbit application OAuth 2.0 Client ID.
fitbit_credentials.ClientSecret | String | "\<Client Secret\>" | The Fitbit application Client Secret.
auth_code | String | "\<Auto-Filled\>" | The Fitbit OAuth 2.0 authentication code, this is automatically managed.
heart_rate_resolution | HeartRateResolution | 1 | The resolution of the heartrate data, `1` is one second, `2` is one minute.
update_interval | TimeSpan | "00:00:30" | The time between heart rate data requests, the format is `hh:mm:ss`, the minimum recommended value is 30 seconds because of the [API rate-limit](#Rate-Limit).
use_utc_timezone_for_requests | Boolean | true | Whether to use UTC for requests, otherwise your local timezone is used, setting this to `false` may cause issues.

## API Limitations
### Rate-Limit
<https://dev.fitbit.com/build/reference/web-api/developer-guide/application-design/#Rate-Limits>
> An application’s rate limit is 150 API requests per hour for each user who has consented to share their data; and it resets at the top of each hour.

### Resolution
The heartrate resolution is limited by the device sending data to the API and further by the API's strict rate-limit, so likely the highest resolution that will be possible is 30 second intervals.

<https://dev.fitbit.com/build/reference/web-api/intraday/get-heartrate-intraday-by-date/#Additional-Information>
> The "1sec" detail-level may not always return data in 1 second sampling outside of a recorded exercise. Sampling is more granular during exercise to provide accuracy.
