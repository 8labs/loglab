# 8labs Loglab

A lightweight, incident collaboration tool designed to streamline debugging and log-sharing in real time.

Pipe or tail from the command line to a browser-based chat where your team can review the output live and collaborate side-by-side.

<!-- Project Demo -->
<img src="resources/images/8labs-loglabs-poc.gif" alt="8labs Loglab Demo" width="600" />

## Features

- Simple CLI-first sharing of logs or stdout
- Easy to use chat interface with unique urls per log/pipe

## Starting a session

Download the appropriate binary cli to the machine you need to debug.

Optionally, you can move the file to the appropriate bin folder, but for now we'll assume you're just using chmod to make it executable and running it from where you are.

```bash
tail -f /var/log/system.log | ./loglab
```


## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the [MPLv2](https://www.mozilla.org/en-US/MPL/2.0/).

I'm open to dual licensing if someone would like to dicuss a specific situation.