import React, { useState, useEffect } from "react";

function WeatherDisplay({ name, temp }: { name: String; temp: Number }) {
  return (
    <>
      <p>
        Nearest location: <strong>{name}</strong>
      </p>
      <p>
        Temperature there: <strong>{temp}&deg;C</strong>
      </p>
    </>
  );
}

function Weather() {
  const [location, setLocation] = useState("");
  const [searching, setSearching] = useState(false);
  const [temp, setTemp] = useState<{ name: String; temp: Number }>();

  useEffect(() => {
    async function find() {
      const response = await fetch(
        `${process.env.REACT_APP_API_URL}/weather/${location}`
      );
      const json = await response.json();
      setTemp({ name: json.name, temp: json.temp });

      setSearching(false);
    }
    if (searching && location) {
      find();
    }
  }, [searching, location]);

  return (
    <div>
      <div>
        <input
          type="text"
          placeholder="Enter an Australia location"
          value={location}
          onChange={e => setLocation(e.target.value)}
        />
        <button onClick={() => setSearching(true)}>Find Weather</button>
        {temp && <WeatherDisplay name={temp.name} temp={temp.temp} />}
      </div>
    </div>
  );
}

export default Weather;
