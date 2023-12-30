data {
  int n;                           // number of observations
  array[n] int<lower=0,upper=1> y; // successes/fails
}

parameters {
  real<lower=0,upper=1> theta;
}

model {
  y ~ bernoulli(theta);
}
