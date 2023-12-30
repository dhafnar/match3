data {
  int n;                           // number of observations
  vector[n] x;                     // independent variable
  int k;                           // number of outcomes
  array[n] int<lower=1,upper=k> y; // dependent variables
}

parameters {
  real beta;      // beta coefficient
  ordered[k-1] c; // cutpoints
}

model {
  beta ~ cauchy(0, 2.5);

  for (i in 1:n)
    y[i] ~ ordered_logistic(x[i] * beta, c);
}
