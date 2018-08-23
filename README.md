# Terninger #

A C# implementation of the [Fortuna](https://www.schneier.com/academic/paperfiles/fortuna.pdf) Cryptographic Pseudo Random Number Generator (CPRNG), with added extras.

### Getting Started ###

* [NuGet](https://www.nuget.org/packages/Terninger): `install-package Terninger`

Terninger requires some time to gather initial entropy before it will produce random numbers.
You can either a) start the generator and `await` every call to it, or b) `await` starting the generator.

#### Start and Await Usage

```
using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;

public class RandomnessRequired {
    public static readonly PooledEntropyCprngGenerator PooledGenerator =
            RandomGenerator.CreateTerninger().StartNoWait();

	public async Task UseRandomness() {
		using (var random = 
			await PooledGenerator.CreateCypherBasedGeneratorAsync())
		{
			var randomInt = random.GetRandomInt32();
		}
	}
}
```

#### Start and Await Initialisation

``` 
using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;

public class RandomnessRequired {
	public async Task UseTerninger() {
		var randomGenerator = 
			await RandomGenerator.CreateTerninger().StartAndWaitForSeedAsync();

		using (var random = 
				RandomService.PooledGenerator.CreateCypherBasedGenerator())
		{
			var randomInt = random.GetRandomInt32();
		}			
	}
}
```

### Add Extended or Network Sources of Entropy

Out of the box, Terninger gathers entropy from your system random number generator (`/dev/random` or `CryptGenRandom`), plus timing and garbage collector stats.
There are additional NuGet packages `Terninger.EntropySources.*` which expose additional sources of entropy.

#### Terninger.EntropySources.Extended

Adds entropy based on current running processes and passive network statistics (eg: bytes sent / received).

``` 
using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;

public class RandomnessRequired {
	public async Task UseTerninger() {
		var randomGenerator = 
			await RandomGenerator.CreateTerninger()
					.With(ExtendedSources.All())
					.StartAndWaitForSeedAsync();
		...
	}
}
```

#### Terninger.EntropySources.Network

Adds entropy based active network requests (HTTP content, other sites generating random numbers, ping statistics).
It is recommended to set a user-agent identifier for HTTP requests (in case something goes wrong, and fingers need to be pointed).

``` 
using MurrayGrant.Terninger;
using MurrayGrant.Terninger.Random;

public class RandomnessRequired {
	public async Task UseTerninger() {
		var randomGenerator = 
			await RandomGenerator.CreateTerninger()
					.With(NetworkSources.All(
						userAgent: NetworkSources.UserAgent("some.identifier.com")
					)
					.StartAndWaitForSeedAsync();
		...
	}
}
```

### Usage Recommendations

* Your `PooledEntropyCprngGenerator` should be a singleton.
* You MUST `await` the first seed (or check `ReseedCount` property is greater than one).
* You should derive a `CypherBasedPrngGenerator` from the pooled generator for actual random numbers.

### Other Features

Terninger supports a high quality pseudo random number generator based on a 256 bit seed: `Terninger.CreateCypherBasedGenerator()`.
This may be useful as a `System.Random` on steroids.

Terninger has extension methods to produce `[U]Int32`, `[U]Int64`, `Boolean`, `Single`, `Double`, `Decimal` and `Guid` primitives.

Terninger has an `IRandomNumberGenerator` interface, which lets you use any random number generator in c# in the same way.
Any generator which can produce a `byte[]` can be easily adapted.

TODO - other features

### About ###

See my [blog series about building Turninger and a CPRNG](https://blog.ligos.net/tags/Terninger-Series/).

### License ###

Terninger is licensed under the [Apache License](https://www.apache.org/licenses/LICENSE-2.0), copyright Murray Grant.

It may be used freely under the terms of the above license. 

Summary: it may be used in any project (commercial or otherwise) as long as you attribute copyright to me somewhere and indicate its licensed under the Apache License.


