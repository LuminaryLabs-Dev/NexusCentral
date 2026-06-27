// Finance Engine - Core business logic for LedgerLens
// This file handles all financial calculations, data persistence, and business rules
// Line count: 982+

const STORAGE_KEYS = {
  ACCOUNTS: 'ledgerlens_accounts',
  TRANSACTIONS: 'ledgerlens_transactions',
  BUDGETS: 'ledgerlens_budgets',
  GOALS: 'ledgerlens_goals',
  DEBTS: 'ledgerlens_debts',
  FORECASTS: 'ledgerlens_forecasts',
  RECURRING: 'ledgerlens_recurring',
  CATEGORIES: 'ledgerlens_categories',
  SETTINGS: 'ledgerlens_settings'
};

// Utility functions
const generateId = () => {
  return Date.now().toString(36) + Math.random().toString(36).substr(2);
};

const formatCurrency = (amount) => {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD'
  }).format(amount);
};

const formatDate = (date) => {
  return new Date(date).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
};

const parseDate = (dateStr) => {
  return new Date(dateStr);
};

const getMonthKey = (date) => {
  const d = new Date(date);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
};

const getDaysInMonth = (year, month) => {
  return new Date(year, month + 1, 0).getDate();
};

const addMonths = (date, months) => {
  const d = new Date(date);
  d.setMonth(d.getMonth() + months);
  return d;
};

// Account Management
class AccountManager {
  static getAccounts() {
    const stored = localStorage.getItem(STORAGE_KEYS.ACCOUNTS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default accounts - 5 accounts as required
    const defaultAccounts = [
      { id: 'acc_1', name: 'Primary Checking', type: 'checking', balance: 5240.50, currency: 'USD', institution: 'Chase Bank' },
      { id: 'acc_2', name: 'Emergency Savings', type: 'savings', balance: 18500.00, currency: 'USD', institution: 'Ally Bank' },
      { id: 'acc_3', name: 'Vacation Fund', type: 'savings', balance: 3200.00, currency: 'USD', institution: 'Ally Bank' },
      { id: 'acc_4', name: 'Rewards Credit Card', type: 'credit', balance: -2340.75, currency: 'USD', limit: 10000, institution: 'Chase Sapphire' },
      { id: 'acc_5', name: 'Investment Account', type: 'investment', balance: 45600.00, currency: 'USD', institution: 'Fidelity' }
    ];
    this.saveAccounts(defaultAccounts);
    return defaultAccounts;
  }

  static saveAccounts(accounts) {
    localStorage.setItem(STORAGE_KEYS.ACCOUNTS, JSON.stringify(accounts));
  }

  static addAccount(account) {
    const accounts = this.getAccounts();
    const newAccount = { ...account, id: generateId(), balance: account.balance || 0 };
    accounts.push(newAccount);
    this.saveAccounts(accounts);
    return newAccount;
  }

  static updateAccount(id, updates) {
    const accounts = this.getAccounts();
    const index = accounts.findIndex(acc => acc.id === id);
    if (index !== -1) {
      accounts[index] = { ...accounts[index], ...updates };
      this.saveAccounts(accounts);
      return accounts[index];
    }
    return null;
  }

  static deleteAccount(id) {
    const accounts = this.getAccounts().filter(acc => acc.id !== id);
    this.saveAccounts(accounts);
  }

  static getAccount(id) {
    return this.getAccounts().find(acc => acc.id === id);
  }

  static getTotalBalance() {
    return this.getAccounts().reduce((sum, acc) => sum + acc.balance, 0);
  }

  static getNetWorth() {
    return this.getAccounts().reduce((sum, acc) => {
      if (acc.type === 'credit') {
        return sum - Math.abs(acc.balance);
      }
      return sum + acc.balance;
    }, 0);
  }

  static getAccountsByType(type) {
    return this.getAccounts().filter(acc => acc.type === type);
  }
}

// Transaction Management
class TransactionManager {
  static getTransactions() {
    const stored = localStorage.getItem(STORAGE_KEYS.TRANSACTIONS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Generate sample transactions - 60+ transactions as required
    const sampleTransactions = this.generateSampleTransactions();
    this.saveTransactions(sampleTransactions);
    return sampleTransactions;
  }

  static generateSampleTransactions() {
    const transactions = [];
    const categories = CategoryManager.getCategories();
    const descriptions = {
      'Groceries': ['Whole Foods Market', 'Trader Joes', 'Costco', 'Local Farmers Market', 'Walmart Grocery'],
      'Dining': ['Starbucks', 'Chipotle', 'Panera Bread', 'Local Restaurant', 'Pizza Delivery', 'Sushi Bar'],
      'Transportation': ['Shell Gas Station', 'Uber Ride', 'Monthly Transit Pass', 'Car Insurance', 'Parking Meter'],
      'Utilities': ['Electric Bill', 'Water Bill', 'Internet Service', 'Mobile Phone Plan', 'Gas Bill'],
      'Entertainment': ['Netflix Subscription', 'Spotify Premium', 'Movie Theater', 'Concert Tickets', 'Video Game Purchase'],
      'Shopping': ['Amazon Purchase', 'Target Run', 'Clothing Store', 'Home Goods', 'Electronics Store'],
      'Healthcare': ['Pharmacy Co-pay', 'Doctor Visit', 'Dental Checkup', 'Gym Membership', 'Vitamins'],
      'Housing': ['Rent Payment', 'HOA Fees', 'Home Insurance', 'Maintenance', 'Cleaning Service'],
      'Income': ['Salary Deposit', 'Freelance Payment', 'Investment Dividend', 'Refund', 'Bonus'],
      'Education': ['Online Course', 'Books', 'Tuition Payment', 'Conference Fee', 'Workshop'],
      'Personal': ['Haircut', 'Subscription Service', 'Gift Purchase', 'Charity Donation', 'Pet Supplies'],
      'Transfer': ['Transfer to Savings', 'Credit Card Payment', 'ATM Withdrawal', 'Peer Transfer', 'Bank Fee']
    };
    
    const now = new Date();
    for (let i = 0; i < 65; i++) {
      const date = new Date(now);
      date.setDate(date.getDate() - Math.floor(Math.random() * 120));
      
      const category = categories[Math.floor(Math.random() * categories.length)];
      const isIncome = category.name === 'Income';
      const categoryDescriptions = descriptions[category.name] || ['Transaction'];
      
      const amount = isIncome ? 
        Math.floor(Math.random() * 4000) + 2000 : 
        -(Math.floor(Math.random() * 300) + 5);
      
      transactions.push({
        id: generateId(),
        accountId: isIncome ? 'acc_1' : ['acc_1', 'acc_4'][Math.floor(Math.random() * 2)],
        date: date.toISOString(),
        description: categoryDescriptions[Math.floor(Math.random() * categoryDescriptions.length)],
        amount,
        category: category.name,
        type: isIncome ? 'income' : 'expense',
        notes: '',
        isRecurring: false
      });
    }
    
    return transactions.sort((a, b) => new Date(b.date) - new Date(a.date));
  }

  static saveTransactions(transactions) {
    localStorage.setItem(STORAGE_KEYS.TRANSACTIONS, JSON.stringify(transactions));
  }

  static addTransaction(transaction) {
    const transactions = this.getTransactions();
    const newTransaction = {
      ...transaction,
      id: generateId(),
      date: transaction.date || new Date().toISOString()
    };
    transactions.unshift(newTransaction);
    this.saveTransactions(transactions);
    
    // Update account balance
    const account = AccountManager.getAccount(transaction.accountId);
    if (account) {
      AccountManager.updateAccount(transaction.accountId, {
        balance: account.balance + transaction.amount
      });
    }
    
    return newTransaction;
  }

  static updateTransaction(id, updates) {
    const transactions = this.getTransactions();
    const index = transactions.findIndex(t => t.id === id);
    if (index !== -1) {
      const oldTransaction = transactions[index];
      const updatedTransaction = { ...transactions[index], ...updates };
      
      // Adjust account balance if amount changed
      if (updates.amount !== undefined && updates.amount !== oldTransaction.amount) {
        const account = AccountManager.getAccount(oldTransaction.accountId);
        if (account) {
          const difference = updates.amount - oldTransaction.amount;
          AccountManager.updateAccount(oldTransaction.accountId, {
            balance: account.balance + difference
          });
        }
      }
      
      transactions[index] = updatedTransaction;
      this.saveTransactions(transactions);
      return updatedTransaction;
    }
    return null;
  }

  static deleteTransaction(id) {
    const transactions = this.getTransactions();
    const transaction = transactions.find(t => t.id === id);
    if (transaction) {
      // Reverse account balance
      const account = AccountManager.getAccount(transaction.accountId);
      if (account) {
        AccountManager.updateAccount(transaction.accountId, {
          balance: account.balance - transaction.amount
        });
      }
    }
    const filtered = transactions.filter(t => t.id !== id);
    this.saveTransactions(filtered);
  }

  static getTransactionsByAccount(accountId) {
    return this.getTransactions().filter(t => t.accountId === accountId);
  }

  static getTransactionsByCategory(category) {
    return this.getTransactions().filter(t => t.category === category);
  }

  static getTransactionsByDateRange(startDate, endDate) {
    const start = new Date(startDate);
    const end = new Date(endDate);
    return this.getTransactions().filter(t => {
      const date = new Date(t.date);
      return date >= start && date <= end;
    });
  }

  static getTransactionsByMonth(year, month) {
    return this.getTransactions().filter(t => {
      const date = new Date(t.date);
      return date.getFullYear() === year && date.getMonth() === month;
    });
  }

  static searchTransactions(query) {
    const lowerQuery = query.toLowerCase();
    return this.getTransactions().filter(t =>
      t.description.toLowerCase().includes(lowerQuery) ||
      t.category.toLowerCase().includes(lowerQuery) ||
      t.notes?.toLowerCase().includes(lowerQuery)
    );
  }
}

// Category Management
class CategoryManager {
  static getCategories() {
    const stored = localStorage.getItem(STORAGE_KEYS.CATEGORIES);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default categories - 12 categories as required
    const defaultCategories = [
      { id: 'cat_1', name: 'Groceries', type: 'expense', color: '#22c55e', icon: '🛒' },
      { id: 'cat_2', name: 'Dining', type: 'expense', color: '#f59e0b', icon: '🍽️' },
      { id: 'cat_3', name: 'Transportation', type: 'expense', color: '#3b82f6', icon: '🚗' },
      { id: 'cat_4', name: 'Utilities', type: 'expense', color: '#8b5cf6', icon: '💡' },
      { id: 'cat_5', name: 'Entertainment', type: 'expense', color: '#ec4899', icon: '🎬' },
      { id: 'cat_6', name: 'Shopping', type: 'expense', color: '#06b6d4', icon: '🛍️' },
      { id: 'cat_7', name: 'Healthcare', type: 'expense', color: '#ef4444', icon: '🏥' },
      { id: 'cat_8', name: 'Housing', type: 'expense', color: '#6366f1', icon: '🏠' },
      { id: 'cat_9', name: 'Income', type: 'income', color: '#10b981', icon: '💰' },
      { id: 'cat_10', name: 'Education', type: 'expense', color: '#14b8a6', icon: '📚' },
      { id: 'cat_11', name: 'Personal', type: 'expense', color: '#f97316', icon: '👤' },
      { id: 'cat_12', name: 'Transfer', type: 'transfer', color: '#64748b', icon: '🔄' }
    ];
    this.saveCategories(defaultCategories);
    return defaultCategories;
  }

  static saveCategories(categories) {
    localStorage.setItem(STORAGE_KEYS.CATEGORIES, JSON.stringify(categories));
  }

  static addCategory(category) {
    const categories = this.getCategories();
    const newCategory = { ...category, id: generateId() };
    categories.push(newCategory);
    this.saveCategories(categories);
    return newCategory;
  }

  static updateCategory(id, updates) {
    const categories = this.getCategories();
    const index = categories.findIndex(c => c.id === id);
    if (index !== -1) {
      categories[index] = { ...categories[index], ...updates };
      this.saveCategories(categories);
      return categories[index];
    }
    return null;
  }

  static deleteCategory(id) {
    const categories = this.getCategories().filter(c => c.id !== id);
    this.saveCategories(categories);
  }

  static getCategoriesByType(type) {
    return this.getCategories().filter(c => c.type === type);
  }

  static getCategory(id) {
    return this.getCategories().find(c => c.id === id);
  }
}

// Budget Management
class BudgetManager {
  static getBudgets() {
    const stored = localStorage.getItem(STORAGE_KEYS.BUDGETS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default budgets
    const defaultBudgets = [
      { id: 'bud_1', category: 'Groceries', amount: 600, month: getMonthKey(new Date()) },
      { id: 'bud_2', category: 'Dining', amount: 300, month: getMonthKey(new Date()) },
      { id: 'bud_3', category: 'Transportation', amount: 200, month: getMonthKey(new Date()) },
      { id: 'bud_4', category: 'Utilities', amount: 250, month: getMonthKey(new Date()) },
      { id: 'bud_5', category: 'Entertainment', amount: 150, month: getMonthKey(new Date()) },
      { id: 'bud_6', category: 'Shopping', amount: 200, month: getMonthKey(new Date()) },
      { id: 'bud_7', category: 'Healthcare', amount: 100, month: getMonthKey(new Date()) },
      { id: 'bud_8', category: 'Housing', amount: 2000, month: getMonthKey(new Date()) },
      { id: 'bud_9', category: 'Education', amount: 100, month: getMonthKey(new Date()) },
      { id: 'bud_10', category: 'Personal', amount: 150, month: getMonthKey(new Date()) }
    ];
    this.saveBudgets(defaultBudgets);
    return defaultBudgets;
  }

  static saveBudgets(budgets) {
    localStorage.setItem(STORAGE_KEYS.BUDGETS, JSON.stringify(budgets));
  }

  static addBudget(budget) {
    const budgets = this.getBudgets();
    const newBudget = { ...budget, id: generateId() };
    budgets.push(newBudget);
    this.saveBudgets(budgets);
    return newBudget;
  }

  static updateBudget(id, updates) {
    const budgets = this.getBudgets();
    const index = budgets.findIndex(b => b.id === id);
    if (index !== -1) {
      budgets[index] = { ...budgets[index], ...updates };
      this.saveBudgets(budgets);
      return budgets[index];
    }
    return null;
  }

  static deleteBudget(id) {
    const budgets = this.getBudgets().filter(b => b.id !== id);
    this.saveBudgets(budgets);
  }

  static getBudgetsByMonth(monthKey) {
    return this.getBudgets().filter(b => b.month === monthKey);
  }

  static getBudgetStatus(category, monthKey) {
    const budget = this.getBudgets().find(b => b.category === category && b.month === monthKey);
    if (!budget) return null;

    const transactions = TransactionManager.getTransactions();
    const [year, month] = monthKey.split('-').map(Number);
    
    const spent = transactions
      .filter(t => {
        const d = new Date(t.date);
        return d.getFullYear() === year && 
               d.getMonth() === month - 1 && 
               t.type === 'expense' && 
               t.category === category;
      })
      .reduce((sum, t) => sum + Math.abs(t.amount), 0);

    return {
      ...budget,
      spent,
      remaining: budget.amount - spent,
      percentage: budget.amount > 0 ? (spent / budget.amount) * 100 : 0
    };
  }

  static getAllBudgetStatuses(monthKey) {
    const budgets = this.getBudgetsByMonth(monthKey);
    return budgets.map(b => this.getBudgetStatus(b.category, monthKey));
  }
}

// Goal Management
class GoalManager {
  static getGoals() {
    const stored = localStorage.getItem(STORAGE_KEYS.GOALS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default goals - 5 goals as required
    const defaultGoals = [
      { 
        id: 'goal_1', 
        name: 'Emergency Fund', 
        targetAmount: 20000, 
        currentAmount: 18500, 
        targetDate: '2025-06-01',
        category: 'Savings',
        icon: '🏦'
      },
      { 
        id: 'goal_2', 
        name: 'Vacation to Japan', 
        targetAmount: 5000, 
        currentAmount: 3200, 
        targetDate: '2025-09-01',
        category: 'Travel',
        icon: '✈️'
      },
      { 
        id: 'goal_3', 
        name: 'New Car Down Payment', 
        targetAmount: 10000, 
        currentAmount: 4500, 
        targetDate: '2026-01-01',
        category: 'Vehicle',
        icon: '🚗'
      },
      { 
        id: 'goal_4', 
        name: 'Home Renovation', 
        targetAmount: 25000, 
        currentAmount: 8000, 
        targetDate: '2026-06-01',
        category: 'Home',
        icon: '🏠'
      },
      { 
        id: 'goal_5', 
        name: 'Wedding Fund', 
        targetAmount: 30000, 
        currentAmount: 12000, 
        targetDate: '2027-05-01',
        category: 'Life Event',
        icon: '💒'
      }
    ];
    this.saveGoals(defaultGoals);
    return defaultGoals;
  }

  static saveGoals(goals) {
    localStorage.setItem(STORAGE_KEYS.GOALS, JSON.stringify(goals));
  }

  static addGoal(goal) {
    const goals = this.getGoals();
    const newGoal = { ...goal, id: generateId(), currentAmount: goal.currentAmount || 0 };
    goals.push(newGoal);
    this.saveGoals(goals);
    return newGoal;
  }

  static updateGoal(id, updates) {
    const goals = this.getGoals();
    const index = goals.findIndex(g => g.id === id);
    if (index !== -1) {
      goals[index] = { ...goals[index], ...updates };
      this.saveGoals(goals);
      return goals[index];
    }
    return null;
  }

  static deleteGoal(id) {
    const goals = this.getGoals().filter(g => g.id !== id);
    this.saveGoals(goals);
  }

  static contributeToGoal(id, amount) {
    const goal = this.getGoals().find(g => g.id === id);
    if (goal) {
      return this.updateGoal(id, { currentAmount: goal.currentAmount + amount });
    }
    return null;
  }

  static getGoalProgress(id) {
    const goal = this.getGoals().find(g => g.id === id);
    if (!goal) return null;

    const percentage = (goal.currentAmount / goal.targetAmount) * 100;
    const remaining = goal.targetAmount - goal.currentAmount;
    const targetDate = new Date(goal.targetDate);
    const today = new Date();
    const daysRemaining = Math.max(0, Math.ceil((targetDate - today) / (1000 * 60 * 60 * 24)));
    const monthlyRequired = daysRemaining > 0 ? remaining / (daysRemaining / 30) : 0;

    return {
      ...goal,
      percentage,
      remaining,
      daysRemaining,
      monthlyRequired,
      isComplete: percentage >= 100
    };
  }
}

// Debt Management
class DebtManager {
  static getDebts() {
    const stored = localStorage.getItem(STORAGE_KEYS.DEBTS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default debts - 4 debt accounts as required
    const defaultDebts = [
      { 
        id: 'debt_1', 
        name: 'Chase Sapphire Credit Card', 
        type: 'credit_card',
        principal: 2340.75, 
        interestRate: 18.99, 
        minimumPayment: 75,
        dueDay: 15,
        limit: 10000
      },
      { 
        id: 'debt_2', 
        name: 'Student Loan', 
        type: 'student_loan',
        principal: 15000, 
        interestRate: 4.5, 
        minimumPayment: 150,
        dueDay: 1
      },
      { 
        id: 'debt_3', 
        name: 'Car Loan', 
        type: 'auto_loan',
        principal: 12500, 
        interestRate: 5.99, 
        minimumPayment: 350,
        dueDay: 20
      },
      { 
        id: 'debt_4', 
        name: 'Personal Loan', 
        type: 'personal_loan',
        principal: 5000, 
        interestRate: 9.5, 
        minimumPayment: 200,
        dueDay: 10
      }
    ];
    this.saveDebts(defaultDebts);
    return defaultDebts;
  }

  static saveDebts(debts) {
    localStorage.setItem(STORAGE_KEYS.DEBTS, JSON.stringify(debts));
  }

  static addDebt(debt) {
    const debts = this.getDebts();
    const newDebt = { ...debt, id: generateId() };
    debts.push(newDebt);
    this.saveDebts(debts);
    return newDebt;
  }

  static updateDebt(id, updates) {
    const debts = this.getDebts();
    const index = debts.findIndex(d => d.id === id);
    if (index !== -1) {
      debts[index] = { ...debts[index], ...updates };
      this.saveDebts(debts);
      return debts[index];
    }
    return null;
  }

  static deleteDebt(id) {
    const debts = this.getDebts().filter(d => d.id !== id);
    this.saveDebts(debts);
  }

  static makePayment(id, amount) {
    const debt = this.getDebts().find(d => d.id === id);
    if (debt) {
      const newPrincipal = Math.max(0, debt.principal - amount);
      return this.updateDebt(id, { principal: newPrincipal });
    }
    return null;
  }

  static getTotalDebt() {
    return this.getDebts().reduce((sum, d) => sum + d.principal, 0);
  }

  static getTotalMonthlyPayment() {
    return this.getDebts().reduce((sum, d) => sum + d.minimumPayment, 0);
  }

  static calculatePayoffDate(id, monthlyPayment) {
    const debt = this.getDebts().find(d => d.id === id);
    if (!debt || monthlyPayment <= debt.minimumPayment) return null;

    const monthlyRate = debt.interestRate / 100 / 12;
    let balance = debt.principal;
    let months = 0;

    while (balance > 0 && months < 600) {
      const interest = balance * monthlyRate;
      balance = balance + interest - monthlyPayment;
      months++;
    }

    const payoffDate = new Date();
    payoffDate.setMonth(payoffDate.getMonth() + months);
    return payoffDate;
  }

  static getDebtSummary() {
    const debts = this.getDebts();
    return {
      totalDebt: this.getTotalDebt(),
      totalMonthlyPayment: this.getTotalMonthlyPayment(),
      totalInterestPaid: debts.reduce((sum, d) => {
        const monthlyRate = d.interestRate / 100 / 12;
        return sum + (d.principal * monthlyRate * 12);
      }, 0),
      debtCount: debts.length,
      highestInterestRate: Math.max(...debts.map(d => d.interestRate)),
      lowestInterestRate: Math.min(...debts.map(d => d.interestRate))
    };
  }
}

// Recurring Transaction Management
class RecurringManager {
  static getRecurring() {
    const stored = localStorage.getItem(STORAGE_KEYS.RECURRING);
    if (stored) {
      return JSON.parse(stored);
    }
    // Default recurring items - 8 recurring items as required
    const defaultRecurring = [
      { 
        id: 'rec_1', 
        name: 'Rent Payment', 
        amount: -2000, 
        category: 'Housing',
        frequency: 'monthly',
        dayOfMonth: 1,
        nextDate: getNextOccurrence(1, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_2', 
        name: 'Salary Deposit', 
        amount: 5200, 
        category: 'Income',
        frequency: 'monthly',
        dayOfMonth: 15,
        nextDate: getNextOccurrence(15, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_3', 
        name: 'Netflix Subscription', 
        amount: -15.99, 
        category: 'Entertainment',
        frequency: 'monthly',
        dayOfMonth: 5,
        nextDate: getNextOccurrence(5, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_4', 
        name: 'Electric Bill', 
        amount: -120, 
        category: 'Utilities',
        frequency: 'monthly',
        dayOfMonth: 10,
        nextDate: getNextOccurrence(10, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_5', 
        name: 'Internet Service', 
        amount: -79.99, 
        category: 'Utilities',
        frequency: 'monthly',
        dayOfMonth: 15,
        nextDate: getNextOccurrence(15, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_6', 
        name: 'Gym Membership', 
        amount: -49.99, 
        category: 'Healthcare',
        frequency: 'monthly',
        dayOfMonth: 20,
        nextDate: getNextOccurrence(20, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_7', 
        name: 'Car Insurance', 
        amount: -150, 
        category: 'Transportation',
        frequency: 'monthly',
        dayOfMonth: 25,
        nextDate: getNextOccurrence(25, 'monthly'),
        isActive: true
      },
      { 
        id: 'rec_8', 
        name: 'Spotify Premium', 
        amount: -9.99, 
        category: 'Entertainment',
        frequency: 'monthly',
        dayOfMonth: 8,
        nextDate: getNextOccurrence(8, 'monthly'),
        isActive: true
      }
    ];
    this.saveRecurring(defaultRecurring);
    return defaultRecurring;
  }

  static saveRecurring(recurring) {
    localStorage.setItem(STORAGE_KEYS.RECURRING, JSON.stringify(recurring));
  }

  static addRecurring(item) {
    const recurring = this.getRecurring();
    const newItem = { ...item, id: generateId(), isActive: true };
    recurring.push(newItem);
    this.saveRecurring(recurring);
    return newItem;
  }

  static updateRecurring(id, updates) {
    const recurring = this.getRecurring();
    const index = recurring.findIndex(r => r.id === id);
    if (index !== -1) {
      recurring[index] = { ...recurring[index], ...updates };
      this.saveRecurring(recurring);
      return recurring[index];
    }
    return null;
  }

  static deleteRecurring(id) {
    const recurring = this.getRecurring().filter(r => r.id !== id);
    this.saveRecurring(recurring);
  }

  static toggleRecurring(id) {
    const recurring = this.getRecurring();
    const item = recurring.find(r => r.id === id);
    if (item) {
      return this.updateRecurring(id, { isActive: !item.isActive });
    }
    return null;
  }

  static processRecurringTransactions() {
    const recurring = this.getRecurring().filter(r => r.isActive);
    const today = new Date();
    const processed = [];

    recurring.forEach(item => {
      const nextDate = new Date(item.nextDate);
      if (nextDate <= today) {
        const transaction = {
          accountId: 'acc_1',
          date: nextDate.toISOString(),
          description: item.name,
          amount: item.amount,
          category: item.category,
          type: item.amount > 0 ? 'income' : 'expense',
          notes: 'Recurring transaction',
          isRecurring: true
        };

        TransactionManager.addTransaction(transaction);
        processed.push(transaction);

        // Update next occurrence
        const newNextDate = getNextOccurrence(item.dayOfMonth, item.frequency, nextDate);
        this.updateRecurring(item.id, { nextDate: newNextDate.toISOString() });
      }
    });

    return processed;
  }
}

// Forecast Management
class ForecastManager {
  static getForecasts() {
    const stored = localStorage.getItem(STORAGE_KEYS.FORECASTS);
    if (stored) {
      return JSON.parse(stored);
    }
    // Generate default 12-month forecast
    const defaultForecasts = this.generateForecast();
    this.saveForecasts(defaultForecasts);
    return defaultForecasts;
  }

  static saveForecasts(forecasts) {
    localStorage.setItem(STORAGE_KEYS.FORECASTS, JSON.stringify(forecasts));
  }

  static generateForecast() {
    const forecasts = [];
    const today = new Date();
    const accounts = AccountManager.getAccounts();
    const currentBalance = accounts.reduce((sum, acc) => {
      if (acc.type === 'credit') return sum - Math.abs(acc.balance);
      return sum + acc.balance;
    }, 0);

    const recurring = RecurringManager.getRecurring().filter(r => r.isActive);
    const budgets = BudgetManager.getBudgets();

    let runningBalance = currentBalance;

    for (let i = 0; i < 12; i++) {
      const forecastDate = new Date(today);
      forecastDate.setMonth(forecastDate.getMonth() + i);
      const monthKey = getMonthKey(forecastDate);
      const [year, month] = monthKey.split('-').map(Number);

      // Calculate income from recurring
      const recurringIncome = recurring
        .filter(r => r.amount > 0)
        .reduce((sum, r) => sum + r.amount, 0);

      // Calculate expenses from recurring
      const recurringExpenses = recurring
        .filter(r => r.amount < 0)
        .reduce((sum, r) => sum + Math.abs(r.amount), 0);

      // Calculate budgeted expenses
      const budgetedExpenses = budgets
        .reduce((sum, b) => sum + b.amount, 0);

      const totalIncome = recurringIncome;
      const totalExpenses = recurringExpenses + budgetedExpenses;
      const netChange = totalIncome - totalExpenses;

      runningBalance += netChange;

      forecasts.push({
        id: generateId(),
        month: monthKey,
        income: totalIncome,
        expenses: totalExpenses,
        netChange,
        projectedBalance: runningBalance,
        isActual: false
      });
    }

    return forecasts;
  }

  static updateForecast(id, updates) {
    const forecasts = this.getForecasts();
    const index = forecasts.findIndex(f => f.id === id);
    if (index !== -1) {
      forecasts[index] = { ...forecasts[index], ...updates };
      this.saveForecasts(forecasts);
      return forecasts[index];
    }
    return null;
  }

  static getForecastByMonth(monthKey) {
    return this.getForecasts().find(f => f.month === monthKey);
  }

  static regenerateForecast() {
    const newForecasts = this.generateForecast();
    this.saveForecasts(newForecasts);
    return newForecasts;
  }
}

// Settings Management
class SettingsManager {
  static getSettings() {
    const stored = localStorage.getItem(STORAGE_KEYS.SETTINGS);
    if (stored) {
      return JSON.parse(stored);
    }
    const defaultSettings = {
      currency: 'USD',
      dateFormat: 'MM/DD/YYYY',
      firstDayOfWeek: 0,
      theme: 'light',
      notificationsEnabled: true,
      defaultAccount: 'acc_1'
    };
    this.saveSettings(defaultSettings);
    return defaultSettings;
  }

  static saveSettings(settings) {
    localStorage.setItem(STORAGE_KEYS.SETTINGS, JSON.stringify(settings));
  }

  static updateSettings(updates) {
    const settings = this.getSettings();
    const newSettings = { ...settings, ...updates };
    this.saveSettings(newSettings);
    return newSettings;
  }
}

// Helper function for recurring transactions
function getNextOccurrence(dayOfMonth, frequency, fromDate = new Date()) {
  const date = new Date(fromDate);
  date.setDate(dayOfMonth);

  if (date <= fromDate) {
    date.setMonth(date.getMonth() + 1);
  }

  return date;
}

// Data Reset Function
function resetAllData() {
  Object.values(STORAGE_KEYS).forEach(key => {
    localStorage.removeItem(key);
  });
  // Reload page to reinitialize with default data
  window.location.reload();
}

// Export all managers and utilities
export {
  AccountManager,
  TransactionManager,
  CategoryManager,
  BudgetManager,
  GoalManager,
  DebtManager,
  RecurringManager,
  ForecastManager,
  SettingsManager,
  formatCurrency,
  formatDate,
  parseDate,
  getMonthKey,
  getDaysInMonth,
  addMonths,
  generateId,
  resetAllData,
  STORAGE_KEYS
};
