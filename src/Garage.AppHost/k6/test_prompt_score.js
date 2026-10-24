import http from 'k6/http';

// Drives the `prompt-file` flag: every request makes the API evaluate the flag for a random user,
// score the served variant (unreliable 0, brief 3, casual 5, expert 10) and report it through
// OpenFeature tracking. Change the flag while this runs and watch the "Prompt Score" dashboard.
export const options = {
  discardResponseBodies: true,
  scenarios: {
    constant: {
      executor: 'constant-arrival-rate',

      // How many iterations per timeUnit
      rate: 10,

      // Start `rate` iterations per second
      timeUnit: '1s',

      // Keep generating load for an hour (or until the resource is stopped)
      duration: '1h',

      // Pre-allocate 2 VUs before starting the test
      preAllocatedVUs: 2,

      // Spin up a maximum of 50 VUs to sustain the defined
      // constant arrival rate.
      maxVUs: 50,
    },
  },
};

export default function () {
  http.get(`${__ENV.services__apiservice__http__0}/test/prompt-score`);
}
